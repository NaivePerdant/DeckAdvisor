using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace DeckAdvisor.DeckAdvisorCode;

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
public static class CardScoreLabelPatch
{
    static void Postfix(NCard __instance)
    {
        __instance.GetNodeOrNull<Label>("_DeckAdvisorScore")?.QueueFree();
        __instance.GetParent()?.GetNodeOrNull<Label>("_DeckAdvisorScore")?.QueueFree();

        var model = __instance.Model;
        if (model == null) return;

        if (FindAncestor<NCardRewardSelectionScreen>(__instance) == null) return;

        if (!CardScorer.Current.ContainsKey(model.Id))
            CardScorer.EvaluateFromReflection(__instance);

        if (!CardScorer.Current.TryGetValue(model.Id, out var result)) return;

        var label = new Label
        {
            Name = "_DeckAdvisorScore",
            Text = $"{result.grade}  {result.score:F1}",
            ZIndex = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Off,
        };
        label.AddThemeColorOverride("font_color", GradeColor(result.grade));
        label.AddThemeFontSizeOverride("font_size", 26);

        var holder = __instance.GetParent() as Control;
        if (holder == null) return;
        holder.AddChild(label);
        var lblSize = label.GetMinimumSize();
        label.Position = new Vector2((NCard.defaultSize.X - lblSize.X) / 2f, 250f);
    }

    static T? FindAncestor<T>(Node node) where T : Node
    {
        var parent = node.GetParent();
        while (parent != null)
        {
            if (parent is T t) return t;
            parent = parent.GetParent();
        }
        return null;
    }

    static Color GradeColor(string grade) => grade switch {
        "S" => new Color(1f, 0.84f, 0f),
        "A" => new Color(0.4f, 1f, 0.4f),
        "B" => new Color(0.4f, 0.8f, 1f),
        "C" => new Color(1f, 1f, 1f),
        _   => new Color(0.6f, 0.6f, 0.6f),
    };
}
