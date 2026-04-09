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
    }
}
