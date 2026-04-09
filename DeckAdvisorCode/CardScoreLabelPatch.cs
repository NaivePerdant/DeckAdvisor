using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace DeckAdvisor.DeckAdvisorCode;

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
public static class CardScoreLabelPatch
{
    static void Postfix(NCard __instance)
    {
        __instance.GetNodeOrNull<Label>("_DeckAdvisorScore")?.QueueFree();

        var model = __instance.Model;
        if (model == null || !CardScorer.Current.TryGetValue(model.Id, out var result))
        {
            MainFile.Logger.Info($"DeckAdvisor: UpdateVisuals called, model={(model == null ? "null" : model.Id.ToString())}, score found={model != null && CardScorer.Current.ContainsKey(model.Id)}");
            return;
        }

        MainFile.Logger.Info($"DeckAdvisor: Adding score label {result.grade} {result.score:F1} to card {model.Id}");

        var label = new Label
        {
            Name = "_DeckAdvisorScore",
            Text = $"{result.grade}  {result.score:F1}",
            Position = new Vector2(10f, 10f),
            ZIndex = 10,
        };
        label.AddThemeColorOverride("font_color", GradeColor(result.grade));
        label.AddThemeFontSizeOverride("font_size", 26);

        __instance.AddChild(label);
    }

    static Color GradeColor(string grade) => grade switch {
        "S" => new Color(1f, 0.84f, 0f),
        "A" => new Color(0.4f, 1f, 0.4f),
        "B" => new Color(0.4f, 0.8f, 1f),
        "C" => new Color(1f, 1f, 1f),
        _   => new Color(0.6f, 0.6f, 0.6f),
    };
}
