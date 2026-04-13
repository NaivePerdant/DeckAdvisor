using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace DeckAdvisor.DeckAdvisorCode;

/// <summary>
/// Harmony Patch：在 NCard.UpdateVisuals 执行后，向卡牌下方添加信息框。
///
/// 触发时机：每次卡牌视觉刷新时（进入选牌界面、鼠标悬停等）。
/// 显示条件：卡牌所在界面是以下之一时才显示：
///   - NCardRewardSelectionScreen（战斗奖励）
///   - NMerchantCard（商店）
///   - NChooseACardSelectionScreen / NSimpleCardSelectScreen（事件选牌）
///   - NDeckUpgradeSelectScreen / NDeckEnchantSelectScreen / NDeckCardSelectScreen（牌组操作）
///
/// 信息框内容由 card_overrides.json 的 note 字段控制。
/// 信息框颜色由算法评级决定：S=橙, A=紫, B/C=蓝, D/F=白。
/// </summary>
[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
public static class CardScoreLabelPatch
{
    const string NodeName = "_DeckAdvisorScore";  // 信息框节点名，用于查找和删除旧框
    const float BoxW = 300f;  // 框宽=卡片宽度（NCard.defaultSize.X）
    const float BoxH = 80f;   // 框高（固定）

    static void Postfix(NCard __instance)
    {
        var model = __instance.Model;
        if (model == null) return;

        var parent = __instance.GetParent();
        if (parent == null) return;

        // 同步删除旧框（避免 UpdateVisuals 多次触发导致重叠）
        var existing = parent.GetNodeOrNull<ColorRect>(NodeName);
        if (existing != null) existing.GetParent().RemoveChild(existing);

        // 判断当前界面类型，不在支持的界面中则跳过
        bool inReward = FindAncestor<NCardRewardSelectionScreen>(__instance) != null;
        bool inShop   = FindAncestorByName(__instance, "NMerchantCard") != null;
        bool inEvent  = FindAncestor<NChooseACardSelectionScreen>(__instance) != null
                     || FindAncestor<NSimpleCardSelectScreen>(__instance) != null
                     || FindAncestor<NDeckUpgradeSelectScreen>(__instance) != null
                     || FindAncestor<NDeckEnchantSelectScreen>(__instance) != null
                     || FindAncestor<NDeckCardSelectScreen>(__instance) != null;
        if (!inReward && !inShop && !inEvent) return;

        // 触发评分（如果该牌还没有缓存的评分）
        if (!CardScorer.Current.ContainsKey(model.Id))
        {
            if (inReward || inEvent) CardScorer.EvaluateFromReflection(__instance);
            else                     CardScorer.EvaluateShopCard(__instance);
        }

        if (!CardScorer.Current.TryGetValue(model.Id, out var result)) return;

        // 根据配置决定显示内容
        string? noteText  = CardOverrides.ShowNote  ? result.note : null;
        string? scoreText = CardOverrides.ShowScore ? $"{result.grade}  {result.score:F1}" : null;

        // 两者都为空则不显示框
        if (string.IsNullOrEmpty(noteText) && string.IsNullOrEmpty(scoreText)) return;

        string text = (scoreText != null && noteText != null) ? $"{scoreText}\n{noteText}"
                    : scoreText ?? noteText ?? "";

        var holder = parent as Control;
        if (holder == null) return;

        // 构建信息框：边框 + 背景 + 文字
        // holder 的原点在卡片中心，所以 X = -BoxW/2 对齐卡片左边
        var gradeColor = GradeColor(result.grade);
        var border = new ColorRect
        {
            Name = NodeName,
            Color = gradeColor,
            Size = new Vector2(BoxW + 4, BoxH + 4),
            Position = new Vector2(-NCard.defaultSize.X / 2f - 2f, 250f),
        };
        var bg = new ColorRect
        {
            Color = new Color(0.05f, 0.05f, 0.05f, 0.93f),
            Position = new Vector2(2, 2),
            Size = new Vector2(BoxW, BoxH),
        };
        border.AddChild(bg);

        // 使用 MegaRichTextLabel.SetTextAutoSize 自动缩字号适应框大小
        var richLabel = new MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel
        {
            Size = new Vector2(BoxW - 8, BoxH - 8),
            Position = new Vector2(4, 4),
            BbcodeEnabled = false,
            MaxFontSize = 26,  // 比游戏卡片描述字号（约28-32）小1-2号
            MinFontSize = 12,
        };
        richLabel.AddThemeColorOverride("default_color", gradeColor);
        richLabel.SetTextAutoSize(text);
        bg.AddChild(richLabel);
        holder.AddChild(border);
    }

    /// <summary>向上遍历场景树，找到指定类型的祖先节点。</summary>
    static T? FindAncestor<T>(Node node) where T : Node
    {
        var p = node.GetParent();
        while (p != null) { if (p is T t) return t; p = p.GetParent(); }
        return null;
    }

    /// <summary>向上遍历场景树，按类型名查找祖先节点（用于 Godot 脚本类）。</summary>
    static Node? FindAncestorByName(Node node, string typeName)
    {
        var p = node.GetParent();
        while (p != null) { if (p.GetType().Name == typeName) return p; p = p.GetParent(); }
        return null;
    }

    /// <summary>
    /// 根据评级返回对应颜色。
    /// S=橙（高优先级），A=紫，B/C=蓝，D/F=白（低优先级）。
    /// </summary>
    static Color GradeColor(string grade) => grade switch
    {
        "S"        => new Color(1.0f, 0.65f, 0.0f),  // 橙
        "A"        => new Color(0.7f, 0.4f, 1.0f),   // 紫
        "B" or "C" => new Color(0.4f, 0.7f, 1.0f),   // 蓝
        _          => new Color(0.9f, 0.9f, 0.9f),   // 白
    };
}
