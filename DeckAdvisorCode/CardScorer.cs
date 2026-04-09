using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace DeckAdvisor.DeckAdvisorCode;

public static class CardScorer
{
    public static readonly Dictionary<ModelId, (float score, string grade)> Current = new();

    public static void Evaluate(Player player, IReadOnlyList<CardModel> options)
    {
        Current.Clear();
        var deck = player.Deck.Cards.ToList();
        foreach (var card in options)
        {
            float s = Score(deck, card);
            string grade = s >= 8f ? "S" : s >= 6f ? "A" : s >= 4f ? "B" : s >= 2f ? "C" : "D";
            Current[card.Id] = (s, grade);
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

            // Collect card models from sibling NCards in the reward screen
            // Try parent, then grandparent to find the container holding all reward cards
            var cards = FindRewardCards(triggerCard);

            if (cards == null || cards.Count == 0)
            {
                MainFile.Logger.Info("DeckAdvisor: No sibling NCards found");
                return;
            }

            MainFile.Logger.Info($"DeckAdvisor: Evaluating {cards.Count} cards.");
            Evaluate(player, cards);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"DeckAdvisor: EvaluateFromReflection failed: {ex.Message}");
        }
    }

    static List<CardModel>? FindRewardCards(MegaCrit.Sts2.Core.Nodes.Cards.NCard triggerCard)
    {
        // Walk up to NCardRewardSelectionScreen and read _options via reflection
        var node = triggerCard.GetParent();
        while (node != null)
        {
            if (node is MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen screen)
            {
                var field = screen.GetType().GetField("_options",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var val = field?.GetValue(screen);
                MainFile.Logger.Info($"DeckAdvisor: _options field={field != null}, val type={val?.GetType().Name ?? "null"}");
                if (val is IReadOnlyList<MegaCrit.Sts2.Core.Entities.Cards.CardCreationResult> options)
                    return options.Select(o => o.Card).ToList();
                return null;
            }
            node = node.GetParent();
        }
        return null;
    }

    private static float Score(List<CardModel> deck, CardModel card)
    {
        float s = card.Rarity switch {
            CardRarity.Rare     => 6f,
            CardRarity.Uncommon => 4f,
            _                   => 2f,
        };

        int attacks = deck.Count(c => c.Type == CardType.Attack);
        int skills  = deck.Count(c => c.Type == CardType.Skill);

        if (card.Type == CardType.Attack && attacks < skills) s += 1.5f;
        if (card.Type == CardType.Skill  && skills < attacks) s += 1.5f;
        if (card.Type == CardType.Power)                      s += 1f;

        if (deck.Count < 15 && card.EnergyCost.Canonical >= 3) s -= 1f;

        return Math.Clamp(s, 0f, 10f);
    }
}
