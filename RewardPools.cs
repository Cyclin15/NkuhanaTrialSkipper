using System.Collections.Generic;
using RoR2;
using RoR2.Quests.TrialQuests;
using static RoR2.RoR2Content.Items;

namespace NkuhanaTrialSkipper;

/// <summary>
/// 各试炼可刷出的全部虚灵（Ethereal）物品种类，对照 riskofrain2.wiki.gg 的
/// N'kuhana Trials 页。用于跳过结算时把铜手（Greater Trial Blessing）的自选列表
/// 最大化：原版规则是"列出本次试炼捡到的虚灵物品选一件永久保留"，即时结算时
/// 什么都没捡，这里改为注入该试炼可能刷出的全部种类，其余规则不变。
///
/// 物品内部名已对照游戏 Addressables 目录逐一核实。
/// 试炼开局直接给予的物品（贪欲图腾、57 叶三叶草、仪式头盔等）不属于虚灵拾取池，不注入。
/// </summary>
internal static class RewardPools
{
    internal static IReadOnlyList<ItemDef> GetPool(BaseTrialQuest quest)
    {
        switch (quest)
        {
            // Trial of the Scavenger's Dream（开箱试炼）
            case TrialQuestSadismOpenChests:
                return new[] { Hoof, StunChanceOnHit, Firework };

            // Trial of Broken Glass（碎灯试炼）
            case TrialQuestSadismBreakTargets:
                return new[] { Hoof, SprintBonus };

            // Trial of Infernal Greed（囤金试炼）
            case TrialQuestSadismMoney:
                return new[]
                {
                    StickyBomb, NearbyDamageBonus, Syringe, IgniteOnKill, Firework,
                    Hoof, GoldOnHit, DLC3Content.Items.SpeedOnPickup, ExecuteLowHealthElite, ChainLightning,
                };

            // Trial of the Agile / Fleet-Footed / Conduit（三条竞速赛道）
            case TrialSpeedrunA:
            case TrialSpeedrunB:
            case TrialSpeedrunC:
                return new[] { Hoof, SprintBonus, Feather, JumpBoost, SprintOutOfCombat };

            // Trial of Causality（触发伤害试炼）
            case TrialQuestSurvivorProcCity:
                return new[]
                {
                    IgniteOnKill, StickyBomb, Dagger, SecondarySkillMagazine, Missile,
                    Thorns, ChainLightning, FireRing, IceRing, GhostOnKill,
                };

            // Trial of Lesser Echoes（杀影魔试炼）
            case TrialQuestSurvivorUmbra:
                return new[]
                {
                    Bear, BarrierOnKill, IgniteOnKill, Hoof, DLC2Content.Items.AttackSpeedPerNearbyAllyOrEnemy,
                    Tooth, SlowOnHit, JumpBoost, ChainLightning, Missile, DLC1Content.Items.PrimarySkillShuriken,
                    KillEliteFrenzy, GhostOnKill, SprintArmor,
                };

            // Trial of the Abyss（喂雾试炼）
            case TrialQuestSurvivorMonstersOffMap:
                return new[]
                {
                    Hoof, DLC3Content.Items.SpeedOnPickup, SprintOutOfCombat,
                    DLC2Content.Items.SpeedBoostPickup, JumpBoost, DLC1Content.Items.MoveSpeedOnKill,
                };

            default:
                return null;
        }
    }
}
