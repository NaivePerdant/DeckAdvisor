using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace DeckAdvisor.DeckAdvisorCode;

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
public static class CardScoreLabelPatch
{
    const string NodeName = "_DeckAdvisorScore";

    static void Postfix(NCard __instance)
    {
        // 同步删除旧框
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

        var holder = __instance.GetParent() as Control;
        if (holder == null) return;

        // 构建文本
        string text = $"{result.grade}  {result.score:F1}";
        if (!string.IsNullOrEmpty(result.note))
            text += $"\n{result.note}";

        bool hasNote = !string.IsNullOrEmpty(result.note);
        float boxW = 190f;
        float boxH = hasNote ? 64f : 36f;
        var gradeColor = GradeColor(result.grade);

        // 边框
        var border = new ColorRect
        {
            Name = NodeName,
            Color = gradeColor,
            Size = new Vector2(boxW + 4, boxH + 4),
            Position = new Vector2((NCard.defaultSize.X - boxW) / 2f - 2f, 250f),
        };
        // 背景
        var bg = new ColorRect
        {
            Color = new Color(0.05f, 0.05f, 0.05f, 0.92f),
            Position = new Vector2(2, 2),
            Size = new Vector2(boxW, boxH),
        };
        border.AddChild(bg);
        // 文字
        var label = new Label
        {
            Text = text,
            Size = new Vector2(boxW, boxH),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeColorOverride("font_color", gradeColor);
        label.AddThemeFontSizeOverride("font_size", hasNote ? 16 : 20);
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

    static Color GradeColor(string grade) => grade switch {
        "S" => new Color(1f, 0.84f, 0f),
        "A" => new Color(0.4f, 1f, 0.4f),
        "B" => new Color(0.4f, 0.8f, 1f),
        "C" => new Color(1f, 1f, 1f),
        _   => new Color(0.6f, 0.6f, 0.6f),
    };
}
