using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace DeckAdvisor.DeckAdvisorCode;

/// <summary>
/// 核心评分引擎。
///
/// 评分流程：
/// 1. 从 CardModel 提取属性（CardAttributeExtractor）
/// 2. 检测牌库中的联动条件（有撕裂/凶恶等时改变评分行为）
/// 3. 调用 CardBaseScorer 计算基础分
/// 4. 用户覆盖（card_overrides.json 的 scoreOverride）优先于算法分
///
/// 结果缓存在 Current 字典中，每次进入战斗时清空（CombatRoomPatch）。
/// </summary>
public static class CardScorer
{
    /// <summary>
    /// 当前选牌界面的评分缓存。
    /// Key = CardModel.Id，Value = (分数, 等级, 备注)。
    /// </summary>
    public static readonly Dictionary<ModelId, (float score, string grade, string? note)> Current = new();

    // ── 联动检测用的牌名集合 ─────────────────────────────────────────────

    /// <summary>AOE牌，影响新AOE牌的边际价值。</summary>
    static readonly HashSet<string> AoeCards = new()
        { "Breakthrough", "Conflagration", "Thunderclap", "Stomp", "HowlFromBeyond", "PactsEnd", "Whirlwind" };

    /// <summary>易伤源，多段牌有易伤源时伤害×1.1。</summary>
    static readonly HashSet<string> VulnerableCards = new()
        { "Bash", "Break", "Bully", "Colossus", "Dismantle", "Dominate", "MoltenFist",
          "Taunt", "Thunderclap", "Tremble", "Uppercut", "Vicious" };

    /// <summary>力量来源，多段牌有力量来源时伤害×1.1。</summary>
    static readonly HashSet<string> StrengthSources = new()
        { "Inflame", "DemonForm", "Rupture", "SetupStrike", "Brand", "FightMe" };

    // ── 公开接口 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 批量评分（奖励界面/事件选牌）。
    /// 先清空缓存，再对所有候选牌评分。
    /// </summary>
    public static void Evaluate(Player player, IReadOnlyList<CardModel> options)
    {
        Current.Clear();
        var deck = player.Deck.Cards.ToList();
        var deckNames = deck.Select(c => c.GetType().Name).ToHashSet();
        int floor = player.RunState?.CurrentActIndex ?? 0;

        foreach (var card in options)
        {
            float s = ScoreCard(card, deck, deckNames, floor);
            string grade = ToGrade(s);
            Current[card.Id] = (s, grade, CardOverrides.GetNote(card.GetType().Name));
        }
    }

    /// <summary>
    /// 单张评分（商店/牌组操作界面）。
    /// 不清空缓存，只评分当前这张牌。
    /// </summary>
    public static void EvaluateShopCard(MegaCrit.Sts2.Core.Nodes.Cards.NCard triggerCard)
    {
        try
        {
            var state = RunManager.Instance.DebugOnlyGetState();
            if (state == null) return;
            var player = state.Players.FirstOrDefault();
            if (player == null) return;
            var card = triggerCard.Model;
            if (card == null) return;

            var deck = player.Deck.Cards.ToList();
            var deckNames = deck.Select(c => c.GetType().Name).ToHashSet();
            int floor = player.RunState?.CurrentActIndex ?? 0;

            float s = ScoreCard(card, deck, deckNames, floor);
            Current[card.Id] = (s, ToGrade(s), CardOverrides.GetNote(card.GetType().Name));
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"DeckAdvisor: EvaluateShopCard failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 通过反射从奖励/事件界面获取候选牌列表，然后批量评分。
    /// 用于 NCard.UpdateVisuals 的 Postfix patch。
    /// </summary>
    public static void EvaluateFromReflection(MegaCrit.Sts2.Core.Nodes.Cards.NCard triggerCard)
    {
        try
        {
            var state = RunManager.Instance.DebugOnlyGetState();
            if (state == null) { MainFile.Logger.Info("DeckAdvisor: RunState is null"); return; }

            var player = state.Players.FirstOrDefault();
            if (player == null) { MainFile.Logger.Info("DeckAdvisor: No players in state"); return; }

            var cards = FindRewardCards(triggerCard);
            if (cards == null || cards.Count == 0) { MainFile.Logger.Info("DeckAdvisor: No reward cards found"); return; }

            MainFile.Logger.Info($"DeckAdvisor: Evaluating {cards.Count} cards.");
            Evaluate(player, cards);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"DeckAdvisor: EvaluateFromReflection failed: {ex.Message}");
        }
    }

    // ── 内部实现 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 对单张牌评分。
    /// 优先使用 card_overrides.json 的 scoreOverride，否则调用 CardBaseScorer。
    /// </summary>
    static float ScoreCard(CardModel card, List<CardModel> deck, HashSet<string> deckNames, int floor)
    {
        string name = card.GetType().Name;

        // 用户手动覆盖优先
        if (CardOverrides.GetScoreOverride(name) is float overrideScore)
            return overrideScore;

        int aoeCount = deckNames.Count(n => AoeCards.Contains(n));

        // 检测联动条件（影响失血/消耗/多段的评分行为）
        bool hasRupture        = deckNames.Contains("Rupture");
        bool hasInferno        = deckNames.Contains("Inferno");
        bool hasTearAsunder    = deckNames.Contains("TearAsunder");
        bool hasAshenStrike    = deckNames.Contains("AshenStrike");
        bool hasFeelNoPain     = deckNames.Contains("FeelNoPain");
        bool hasStrengthSource = deckNames.Any(n => StrengthSources.Contains(n));
        bool hasVulnSource     = deckNames.Any(n => VulnerableCards.Contains(n));

        return CardBaseScorer.Calculate(card, aoeCount,
            hasRupture, hasInferno, hasTearAsunder,
            hasAshenStrike, hasFeelNoPain,
            hasStrengthSource, hasVulnSource);
    }

    /// <summary>
    /// 将分数转换为等级字符串。
    /// S≥9, A≥7, B≥5, C≥3, D≥0, F<0
    /// </summary>
    static string ToGrade(float s) =>
        s >= 9f ? "S" : s >= 7f ? "A" : s >= 5f ? "B" : s >= 3f ? "C" : s >= 0f ? "D" : "F";

    /// <summary>
    /// 从触发 NCard 向上遍历场景树，找到选牌界面并读取候选牌列表。
    /// 支持：奖励界面、事件选牌、牌组操作界面。
    /// </summary>
    static List<CardModel>? FindRewardCards(MegaCrit.Sts2.Core.Nodes.Cards.NCard triggerCard)
    {
        var node = triggerCard.GetParent();
        while (node != null)
        {
            // 战斗奖励界面：候选牌在 _options（CardCreationResult 列表）
            if (node is MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen screen)
            {
                var field = screen.GetType().GetField("_options",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(screen) is IReadOnlyList<MegaCrit.Sts2.Core.Entities.Cards.CardCreationResult> options)
                    return options.Select(o => o.Card).ToList();
                return null;
            }

            // 事件/牌组操作界面：候选牌在父类 NCardGridSelectionScreen._cards
            // 使用 FlattenHierarchy 搜索父类的 protected 字段
            if (node is MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NChooseACardSelectionScreen
             || node is MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NSimpleCardSelectScreen
             || node is MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NDeckCardSelectScreen
             || node is MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NDeckUpgradeSelectScreen
             || node is MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NDeckEnchantSelectScreen)
            {
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                          | System.Reflection.BindingFlags.FlattenHierarchy;
                var field = node.GetType().GetField("_cards", flags);
                var val = field?.GetValue(node);
                if (val is IReadOnlyList<CardModel> cards) return cards.ToList();
                if (val is List<CardModel> cards2) return cards2;
                return null;
            }

            node = node.GetParent();
        }
        return null;
    }
}

/// <summary>
/// 每次进入战斗时清空评分缓存。
/// 防止上一场战斗的评分数据影响新战斗的选牌建议。
/// </summary>
[HarmonyLib.HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom), "_Ready")]
public static class CombatRoomPatch
{
    static void Postfix() => CardScorer.Current.Clear();
}
