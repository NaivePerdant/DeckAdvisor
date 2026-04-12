using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace DeckAdvisor.DeckAdvisorCode;

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
public static class CardScoreLabelPatch
{
    const string NodeName = "_DeckAdvisorScore";
    // 框宽等于卡片宽度，左右对齐卡片边框（holder 坐标系，scale=0.8）
    const float BoxW = 300f;
    const float BoxH = 80f;

    static void Postfix(NCard __instance)
    {
        var model = __instance.Model;
        if (model == null) return;

        var parent = __instance.GetParent();
        if (parent == null) return;

        var existing = parent.GetNodeOrNull<ColorRect>(NodeName);
        if (existing != null) existing.GetParent().RemoveChild(existing);

        bool inReward  = FindAncestor<NCardRewardSelectionScreen>(__instance) != null;
        bool inShop    = FindAncestorByName(__instance, "NMerchantCard") != null;
        bool inEvent   = FindAncestor<MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NChooseACardSelectionScreen>(__instance) != null
                      || FindAncestor<MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NSimpleCardSelectScreen>(__instance) != null
                      || FindAncestor<MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NDeckUpgradeSelectScreen>(__instance) != null
                      || FindAncestor<MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NDeckEnchantSelectScreen>(__instance) != null
                      || FindAncestor<MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NDeckCardSelectScreen>(__instance) != null;
        if (!inReward && !inShop && !inEvent) return;

        if (!CardScorer.Current.ContainsKey(model.Id))
        {
            if (inReward || inEvent) CardScorer.EvaluateFromReflection(__instance);
            else                     CardScorer.EvaluateShopCard(__instance);
        }

        if (!CardScorer.Current.TryGetValue(model.Id, out var result)) return;

        string? noteText  = CardOverrides.ShowNote  ? result.note : null;
        string? scoreText = CardOverrides.ShowScore ? $"{result.grade}  {result.score:F1}" : null;

        if (string.IsNullOrEmpty(noteText) && string.IsNullOrEmpty(scoreText)) return;

        string text = (scoreText != null && noteText != null) ? $"{scoreText}\n{noteText}"
                    : scoreText ?? noteText ?? "";

        var holder = __instance.GetParent() as Control;
        if (holder == null) return;

        var gradeColor = GradeColor(result.grade);
        // holder 原点在卡片中心，卡片左边 X = -defaultSize.X/2
        float cardLeft = -NCard.defaultSize.X / 2f;
        var border = new ColorRect
        {
            Name = NodeName,
            Color = gradeColor,
            Size = new Vector2(BoxW + 4, BoxH + 4),
            Position = new Vector2(cardLeft - 2f, 250f),
        };
        var bg = new ColorRect
        {
            Color = new Color(0.05f, 0.05f, 0.05f, 0.93f),
            Position = new Vector2(2, 2),
            Size = new Vector2(BoxW, BoxH),
        };
        border.AddChild(bg);

        var richLabel = new MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel
        {
            Size = new Vector2(BoxW - 8, BoxH - 8),
            Position = new Vector2(4, 4),
            BbcodeEnabled = false,
            MaxFontSize = 26,
            MinFontSize = 12,
        };
        richLabel.AddThemeColorOverride("default_color", GradeColor(result.grade));
        richLabel.SetTextAutoSize(text);
        bg.AddChild(richLabel);
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

    // 四个等级：D/F=白，C/B=蓝，A=紫，S=橙
    static Color GradeColor(string grade) => grade switch
    {
        "S"           => new Color(1.0f, 0.65f, 0.0f),   // 橙
        "A"           => new Color(0.7f, 0.4f, 1.0f),    // 紫
        "B" or "C"    => new Color(0.4f, 0.7f, 1.0f),    // 蓝
        _             => new Color(0.9f, 0.9f, 0.9f),    // 白
    };
}
