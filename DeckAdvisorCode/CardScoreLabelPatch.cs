using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace DeckAdvisor.DeckAdvisorCode;

[HarmonyPatch(typeof(NCard), "Reload")]
public static class CardScoreLabelPatch
{
    static void Postfix(NCard __instance)
    {
        // Remove any existing score label from a previous Reload
        __instance.GetNodeOrNull<Label>("_DeckAdvisorScore")?.QueueFree();

        var model = __instance.Model;
        if (model == null || !CardScorer.Current.TryGetValue(model.Id, out var result))
            return;

        var label = new Label
        {
            Name = "_DeckAdvisorScore",
            Text = $"{result.grade}  {result.score:F1}",
            // TODO: adjust position after seeing it in-game
            Position = new Vector2(10f, 10f),
            ZIndex = 10,
        };
        label.AddThemeColorOverride("font_color", GradeColor(result.grade));
        label.AddThemeFontSizeOverride("font_size", 26);

        __instance.AddChild(label);
    }

    static Color GradeColor(string grade) => grade switch {
        "S" => new Color(1f, 0.84f, 0f),    // gold
        "A" => new Color(0.4f, 1f, 0.4f),   // green
        "B" => new Color(0.4f, 0.8f, 1f),   // blue
        "C" => new Color(1f, 1f, 1f),        // white
        _   => new Color(0.6f, 0.6f, 0.6f), // gray
    };
}
