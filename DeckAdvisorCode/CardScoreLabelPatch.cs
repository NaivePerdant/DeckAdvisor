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
        MainFile.Logger.Info($"DeckAdvisor: Postfix triggered card={__instance.Model?.GetType().Name ?? "null"} parent={__instance.GetParent()?.GetType().Name ?? "null"}");

        // 同步删除旧标签，避免 QueueFree 异步导致重叠
        var existing = __instance.GetParent()?.GetNodeOrNull<Label>("_DeckAdvisorScore");
        if (existing != null) existing.GetParent().RemoveChild(existing);

        var model = __instance.Model;
        if (model == null) return;

        // 奖励界面 或 商店界面 均显示评分
        bool inReward = FindAncestor<NCardRewardSelectionScreen>(__instance) != null;
        bool inShop   = FindAncestorByName(__instance, "NMerchantCard") != null;
        MainFile.Logger.Info($"DeckAdvisor: card={model.GetType().Name} inReward={inReward} inShop={inShop} ancestors={GetAncestorChain(__instance)}");
        if (!inReward && !inShop) return;

        if (!CardScorer.Current.ContainsKey(model.Id))
        {
            if (inReward)
                CardScorer.EvaluateFromReflection(__instance);
            else
                CardScorer.EvaluateShopCard(__instance);
        }

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

    static Node? FindAncestorByName(Node node, string typeName)
    {
        var parent = node.GetParent();
        while (parent != null)
        {
            if (parent.GetType().Name == typeName) return parent;
            parent = parent.GetParent();
        }
        return null;
    }

    static string GetAncestorChain(Node node)
    {
        var parts = new System.Text.StringBuilder();
        var parent = node.GetParent();
        int depth = 0;
        while (parent != null && depth < 8)
        {
            parts.Append(parent.GetType().Name).Append(" > ");
            parent = parent.GetParent();
            depth++;
        }
        return parts.ToString();
    }

    static Color GradeColor(string grade) => grade switch {
        "S" => new Color(1f, 0.84f, 0f),
        "A" => new Color(0.4f, 1f, 0.4f),
        "B" => new Color(0.4f, 0.8f, 1f),
        "C" => new Color(1f, 1f, 1f),
        _   => new Color(0.6f, 0.6f, 0.6f),
    };
}
