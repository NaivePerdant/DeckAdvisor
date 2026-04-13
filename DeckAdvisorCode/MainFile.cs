using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace DeckAdvisor.DeckAdvisorCode;

/// <summary>
/// Mod 入口。由游戏 ModManager 在启动时调用 Initialize()。
/// </summary>
[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "DeckAdvisor";

    /// <summary>全局日志实例，其他类通过此属性写日志。</summary>
    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        // 注册所有 HarmonyPatch（CardScoreLabelPatch、CombatRoomPatch）
        new Harmony(ModId).PatchAll();

        // 加载 card_overrides.json（与 dll 同目录）
        var modDir = System.IO.Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
        CardOverrides.Load(modDir);

        Logger.Info("DeckAdvisor initialized.");
    }
}
