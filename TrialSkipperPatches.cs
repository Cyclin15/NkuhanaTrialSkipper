using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RoR2;
using RoR2.Quests;
using RoR2.Quests.TrialQuests;
using UnityEngine;
using UnityEngine.Networking;

namespace NkuhanaTrialSkipper;

/// <summary>
/// 跳过 N'kuhana 试炼（the Winnowing）的补丁。
///
/// 游戏原始流程：试炼任务在 BaseTrialQuest.OnEnter 启动，任务成功时调用
/// BaseQuest.EndQuest(true)，QuestRunner 在随后的 Update 里调用任务 OnExit，
/// OnExit 内完成全部结算（聊天播报、拾取清扫波、重生的锣、N'kuhana 宝箱三只手奖励、
/// 剩余 Reckoning 时间回存 DLC4TrialSelector）。
///
/// 本补丁走同一条原生结算路径，保证奖励与原版完全一致：
/// 1. OnEnter 后缀：登记该任务待跳过（此时 QuestRunner 还未正式激活它，不能当场结算）。
/// 2. QuestRunner.Update 后缀：任务激活后的第一帧调用 EndQuest(true) 立即成功。
/// 3. OnExit 后缀：结算完成后按档位直接设置剩余 Reckoning 时间：
///    210 → 150 → 90 → 30（第 n 次跳过 = 总预算 − 60×n）。
///    触发门槛：当前剩余 ≥ 60 秒，否则本次试炼正常进行（即一局最多触发 3 次）。
/// 4. 结算前把该试炼可能刷出的全部虚灵物品种类注入宝箱铜手组件的拾取记录
///    （CreateItemPotentialFromPickedUpItems._pickedUpItems），使铜手
///    （Greater Trial Blessing）的自选列表最大化（可通过配置关闭）。
/// </summary>
[HarmonyPatch]
internal static class TrialSkipperPatches
{
    private static readonly HashSet<BaseTrialQuest> ArmedQuests = new HashSet<BaseTrialQuest>();
    private static readonly HashSet<BaseTrialQuest> SkippedQuests = new HashSet<BaseTrialQuest>();

    private static readonly MethodInfo EndQuestMethod = AccessTools.Method(typeof(BaseQuest), "EndQuest");

    // 铜手组件的拾取记录是私有字段，运行时程序集未公开化，只能反射读写。
    private static readonly FieldInfo PickedUpItemsField =
        AccessTools.Field(typeof(CreateItemPotentialFromPickedUpItems), "_pickedUpItems");

    // 按局计数的跳过档位；selector 实例每局更换，引用变化即视为新的一局。
    private static DLC4TrialSelector _trackedSelector;
    private static int _skipsThisRun;

    [HarmonyPatch(typeof(BaseTrialQuest), nameof(BaseTrialQuest.OnEnter))]
    [HarmonyPostfix]
    private static void ArmQuestOnEnter(BaseTrialQuest __instance)
    {
        if (!NetworkServer.active)
        {
            return;
        }

        ArmedQuests.Add(__instance);
    }

    [HarmonyPatch(typeof(QuestRunner), "Update")]
    [HarmonyPostfix]
    private static void TriggerSkipOnQuestActive(QuestRunner __instance)
    {
        if (!NetworkServer.active || ArmedQuests.Count == 0)
        {
            return;
        }

        if (__instance.quest is BaseTrialQuest quest && ArmedQuests.Remove(quest))
        {
            TrySkipQuest(quest);
        }
    }

    [HarmonyPatch(typeof(BaseTrialQuest), nameof(BaseTrialQuest.OnExit))]
    [HarmonyPostfix]
    private static void ApplyTimeCostOnExit(BaseTrialQuest __instance)
    {
        if (!NetworkServer.active)
        {
            return;
        }

        ArmedQuests.Remove(__instance);
        if (!SkippedQuests.Remove(__instance))
        {
            return;
        }

        DLC4TrialSelector trialSelector = GetTrialSelector();
        if (!trialSelector)
        {
            NkuhanaTrialSkipperPlugin.Log.LogWarning("Time cost not applied: DLC4TrialSelector not found.");
            return;
        }

        EnsureRunTracking(trialSelector);
        _skipsThisRun++;
        float cost = NkuhanaTrialSkipperPlugin.TimeCostSeconds.Value;
        float target = Mathf.Max(DLC4TrialSelector.totalAllotedSecondsForTrials - cost * _skipsThisRun, 0f);
        trialSelector.SetRemainingTrialTimeServer(target);
        NkuhanaTrialSkipperPlugin.Log.LogInfo(
            $"Trial skip settled (#{_skipsThisRun} this run), Reckoning set to {target:F0}s.");
    }

    private static void TrySkipQuest(BaseTrialQuest quest)
    {
        if (!NkuhanaTrialSkipperPlugin.Enabled.Value)
        {
            return;
        }

        DLC4TrialSelector trialSelector = GetTrialSelector();
        if (!trialSelector)
        {
            NkuhanaTrialSkipperPlugin.Log.LogWarning("Skip not triggered: DLC4TrialSelector not found.");
            return;
        }

        float remaining = trialSelector.GetRemainingTrialTimeServer();
        float cost = NkuhanaTrialSkipperPlugin.TimeCostSeconds.Value;
        if (remaining < cost)
        {
            NkuhanaTrialSkipperPlugin.Log.LogInfo(
                $"Skip not triggered: Reckoning remaining {remaining:F1}s is below the required {cost:F0}s, playing the trial normally.");
            return;
        }

        EnsureRunTracking(trialSelector);
        float target = Mathf.Max(DLC4TrialSelector.totalAllotedSecondsForTrials - cost * (_skipsThisRun + 1), 0f);
        SkippedQuests.Add(quest);
        InjectRewardPool(quest);
        EndQuestMethod.Invoke(quest, new object[] { true });
        NkuhanaTrialSkipperPlugin.Log.LogInfo(
            $"Trial skip triggered: instant success settlement, Reckoning will be set to {target:F0}s on settle (currently {remaining:F1}s).");
    }

    /// <summary>
    /// 把该试炼可能刷出的全部虚灵物品种类注入铜手（Greater Trial Blessing）组件的
    /// 拾取记录。原版铜手规则是"从本次试炼实际拾取的虚灵物品中选一件永久保留"，
    /// 即时结算时什么都没捡，自选列表会是空的；注入后列表覆盖该试炼的全部种类，
    /// 其余规则（选一件、永久保留）不变。宝箱组件在整个试炼期间都存在于场景中
    /// （它靠订阅拾取事件来记录），因此结算前注入即可。
    /// </summary>
    private static void InjectRewardPool(BaseTrialQuest quest)
    {
        if (!NkuhanaTrialSkipperPlugin.MaximizeBronzeChoices.Value)
        {
            return;
        }

        IReadOnlyList<ItemDef> pool = RewardPools.GetPool(quest);
        if (pool == null || pool.Count == 0)
        {
            NkuhanaTrialSkipperPlugin.Log.LogInfo(
                $"No reward pool mapped for {quest.GetType().Name}, bronze hand stays vanilla.");
            return;
        }

        CreateItemPotentialFromPickedUpItems[] chestRewards =
            UnityEngine.Object.FindObjectsOfType<CreateItemPotentialFromPickedUpItems>(true);
        if (chestRewards.Length == 0)
        {
            NkuhanaTrialSkipperPlugin.Log.LogWarning(
                "Bronze hand not maximized: no CreateItemPotentialFromPickedUpItems found in scene.");
            return;
        }

        foreach (CreateItemPotentialFromPickedUpItems chestReward in chestRewards)
        {
            if (PickedUpItemsField.GetValue(chestReward) is not HashSet<PickupIndex> pickedUpItems)
            {
                continue;
            }

            int added = 0;
            foreach (ItemDef itemDef in pool)
            {
                if (!itemDef)
                {
                    continue;
                }

                PickupIndex pickupIndex = PickupCatalog.FindPickupIndex(itemDef.itemIndex);
                if (pickupIndex.isValid && pickedUpItems.Add(pickupIndex))
                {
                    added++;
                }
            }

            NkuhanaTrialSkipperPlugin.Log.LogInfo(
                $"Bronze hand choice list maximized: injected {added}/{pool.Count} ethereal item types.");
        }
    }

    private static DLC4TrialSelector GetTrialSelector()
    {
        DLC4ProgressionController progressionController = DLC4ProgressionController.instance;
        if (!progressionController)
        {
            return null;
        }

        return progressionController.trialSelector;
    }

    private static void EnsureRunTracking(DLC4TrialSelector trialSelector)
    {
        if (!ReferenceEquals(_trackedSelector, trialSelector))
        {
            _trackedSelector = trialSelector;
            _skipsThisRun = 0;
        }
    }
}
