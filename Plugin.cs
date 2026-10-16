using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace NkuhanaTrialSkipper;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class NkuhanaTrialSkipperPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.zcode.NkuhanaTrialSkipper";
    public const string PluginName = "NkuhanaTrialSkipper";
    public const string PluginVersion = "1.0.0";

    internal static ConfigEntry<bool> Enabled { get; private set; }
    internal static ConfigEntry<float> TimeCostSeconds { get; private set; }
    internal static ConfigEntry<bool> MaximizeBronzeChoices { get; private set; }
    internal static ManualLogSource Log { get; private set; }

    private void Awake()
    {
        Log = Logger;

        Enabled = Config.Bind("Settings", "Enabled", true,
            "总开关。开启后每次进入 N'kuhana 试炼（Hallowed Concepts DLC）时立即按成功结算。");

        TimeCostSeconds = Config.Bind("Settings", "TimeCostSeconds", 60f,
            "每次跳过结算后，Reckoning 剩余时间直接设置到的档位差（每局总预算 210 秒）：210 → 150 → 90 → 30。剩余时间不足该值时不触发跳过，试炼正常进行。");

        MaximizeBronzeChoices = Config.Bind("Settings", "MaximizeBronzeChoices", true,
            "跳过结算时，把铜手（Greater Trial Blessing）的自选列表最大化：注入该试炼可能刷出的全部虚灵物品种类（原版规则是只列出本次试炼实际捡到的）。");

        Harmony.CreateAndPatchAll(typeof(TrialSkipperPatches));

        RegisterRiskOfOptions();

        Log.LogInfo($"{PluginName} v{PluginVersion} loaded.");
    }

    // 仅在 Risk of Options 存在时调用；JIT 按方法惰性编译，缺依赖时不会执行到这里。
    private void RegisterRiskOfOptions()
    {
        if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions"))
        {
            return;
        }

        RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(Enabled));
        RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.SliderOption(
            TimeCostSeconds, new RiskOfOptions.OptionConfigs.SliderConfig { min = 0f, max = 210f }));
        RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(MaximizeBronzeChoices));
    }
}
