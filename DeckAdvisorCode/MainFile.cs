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
        var modDir = System.IO.Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
        CardOverrides.Load(modDir);
        Logger.Info("DeckAdvisor initialized.");
    }

    static bool _dumped = false;
    public static void TryDumpAllCardNames()
    {
        if (_dumped) return;
        _dumped = true;
        try
        {
            foreach (var pool in MegaCrit.Sts2.Core.Models.ModelDb.AllCardPools)
                foreach (var card in pool.AllCards)
                    Logger.Info($"CARD_NAME: {card.GetType().Name}\t{card.Title}\t{pool.GetType().Name}");
        }
        catch (Exception ex) { Logger.Info($"DeckAdvisor: DumpAllCardNames failed: {ex.Message}"); }
    }
}
