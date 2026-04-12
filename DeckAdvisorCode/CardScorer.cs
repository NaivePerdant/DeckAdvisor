using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace DeckAdvisor.DeckAdvisorCode;

public static class CardScorer
{
    public static readonly Dictionary<ModelId, (float score, string grade, string? note)> Current = new();

    // AOE 牌
    static readonly HashSet<string> AoeCards = new()
        { "Breakthrough", "Conflagration", "Thunderclap", "Stomp", "HowlFromBeyond", "PactsEnd", "Whirlwind" };

    // 易伤源
    static readonly HashSet<string> VulnerableCards = new()
        { "Bash", "Break", "Bully", "Colossus", "Dismantle", "Dominate", "MoltenFist",
          "Taunt", "Thunderclap", "Tremble", "Uppercut", "Vicious" };

    // 力量来源（用于多段×1.1判断）
    static readonly HashSet<string> StrengthSources = new()
        { "Inflame", "DemonForm", "Rupture", "SetupStrike", "Brand", "FightMe" };

    // ── 公开接口 ─────────────────────────────────────────────────────────
    public static void Evaluate(Player player, IReadOnlyList<CardModel> options)
    {
        Current.Clear();
        var deck = player.Deck.Cards.ToList();
        var deckNames = deck.Select(c => c.GetType().Name).ToHashSet();
        int floor = player.RunState?.CurrentActIndex ?? 0;

        foreach (var card in options)
        {
            float s = ScoreCard(card, deck, deckNames, floor);
            string grade = s >= 9f ? "S" : s >= 7f ? "A" : s >= 5f ? "B" : s >= 3f ? "C" : s >= 0f ? "D" : "F";
            Current[card.Id] = (s, grade, CardOverrides.GetNote(card.GetType().Name));
        }
    }

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
            string grade = s >= 9f ? "S" : s >= 7f ? "A" : s >= 5f ? "B" : s >= 3f ? "C" : s >= 0f ? "D" : "F";
            Current[card.Id] = (s, grade, CardOverrides.GetNote(card.GetType().Name));
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"DeckAdvisor: EvaluateShopCard failed: {ex.Message}");
        }
    }

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

    // ── 核心评分逻辑 ─────────────────────────────────────────────────────
    static float ScoreCard(CardModel card, List<CardModel> deck, HashSet<string> deckNames, int floor)
    {
        string name = card.GetType().Name;

        if (CardOverrides.GetScoreOverride(name) is float overrideScore)
            return overrideScore;

        int aoeCount = deckNames.Count(n => AoeCards.Contains(n));

        bool hasRupture       = deckNames.Contains("Rupture");
        bool hasInferno       = deckNames.Contains("Inferno");
        bool hasTearAsunder   = deckNames.Contains("TearAsunder");
        bool hasAshenStrike   = deckNames.Contains("AshenStrike");
        bool hasFeelNoPain    = deckNames.Contains("FeelNoPain");
        bool hasStrengthSource= deckNames.Any(n => StrengthSources.Contains(n));
        bool hasVulnSource    = deckNames.Any(n => VulnerableCards.Contains(n));

        return CardBaseScorer.Calculate(card, aoeCount,
            hasRupture, hasInferno, hasTearAsunder,
            hasAshenStrike, hasFeelNoPain,
            hasStrengthSource, hasVulnSource);
    }

    static List<CardModel>? FindRewardCards(MegaCrit.Sts2.Core.Nodes.Cards.NCard triggerCard)
    {
        var node = triggerCard.GetParent();
        while (node != null)
        {
            // 奖励界面
            if (node is MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen screen)
            {
                var field = screen.GetType().GetField("_options",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(screen) is IReadOnlyList<MegaCrit.Sts2.Core.Entities.Cards.CardCreationResult> options)
                    return options.Select(o => o.Card).ToList();
                return null;
            }
            // 事件选牌界面（NChooseACardSelectionScreen / NSimpleCardSelectScreen / NDeckCardSelectScreen 等）
            // _cards 字段在父类 NCardGridSelectionScreen 里，需要 FlattenHierarchy
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
