using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace DeckAdvisor.DeckAdvisorCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "DeckAdvisor";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        new Harmony(ModId).PatchAll();
        // 加载用户自定义覆盖（card_overrides.json 与 dll 同目录）
        var modDir = System.IO.Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
        CardOverrides.Load(modDir);
        Logger.Info("DeckAdvisor initialized.");
    }
}
