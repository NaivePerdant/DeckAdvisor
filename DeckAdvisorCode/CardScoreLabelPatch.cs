using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace DeckAdvisor.DeckAdvisorCode;

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
public static class CardScoreLabelPatch
{
    const string NodeName = "_DeckAdvisorScore";
    const float BoxW = 190f;
    const float BoxH = 64f;

    static void Postfix(NCard __instance)
    {
        var existing = __instance.GetParent()?.GetNodeOrNull<ColorRect>(NodeName);
        if (existing != null) existing.GetParent().RemoveChild(existing);

        var model = __instance.Model;
        if (model == null) return;

        bool inReward = FindAncestor<NCardRewardSelectionScreen>(__instance) != null;
        bool inShop   = FindAncestorByName(__instance, "NMerchantCard") != null;
        if (!inReward && !inShop) return;

        if (!CardScorer.Current.ContainsKey(model.Id))
        {
            if (inReward) CardScorer.EvaluateFromReflection(__instance);
            else          CardScorer.EvaluateShopCard(__instance);
        }

        if (!CardScorer.Current.TryGetValue(model.Id, out var result)) return;

        string? noteText  = CardOverrides.ShowNote  ? result.note : null;
        string? scoreText = CardOverrides.ShowScore ? $"{result.grade}  {result.score:F1}" : null;

        if (string.IsNullOrEmpty(noteText) && string.IsNullOrEmpty(scoreText)) return;

        string text = (scoreText != null && noteText != null) ? $"{scoreText}\n{noteText}"
                    : scoreText ?? noteText ?? "";

        var holder = __instance.GetParent() as Control;
        if (holder == null) return;

        var border = new ColorRect
        {
            Name = NodeName,
            Color = new Color(0.25f, 0.25f, 0.25f, 0.9f),
            Size = new Vector2(BoxW + 4, BoxH + 4),
            Position = new Vector2((NCard.defaultSize.X - BoxW) / 2f - 2f, 250f),
        };
        var bg = new ColorRect
        {
            Color = new Color(0.05f, 0.05f, 0.05f, 0.93f),
            Position = new Vector2(2, 2),
            Size = new Vector2(BoxW, BoxH),
        };
        border.AddChild(bg);
        var label = new Label
        {
            Text = text,
            Size = new Vector2(BoxW - 8, BoxH - 8),
            Position = new Vector2(4, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ClipText = false,
        };
        label.AddThemeColorOverride("font_color", new Color(1f, 1f, 0.85f, 1f));
        label.AddThemeFontSizeOverride("font_size", 15);
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 1));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        bg.AddChild(label);
        holder.AddChild(border);
    }

    static T? FindAncestor<T>(Node node) where T : Node
    {
        var p = node.GetParent();
        while (p != null) { if (p is T t) return t; p = p.GetParent(); }
        return null;
    }

    static Node? FindAncestorByName(Node node, string typeName)
    {
        var p = node.GetParent();
        while (p != null) { if (p.GetType().Name == typeName) return p; p = p.GetParent(); }
        return null;
    }
}
