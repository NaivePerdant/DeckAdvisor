using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace DeckAdvisor.DeckAdvisorCode;

public static class CardScorer
{
    // Populated just before the card reward screen opens; cleared on next reward
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

        // Penalize high-cost cards in small decks
        if (deck.Count < 15 && card.EnergyCost.BaseValue >= 3) s -= 1f;

        return Math.Clamp(s, 0f, 10f);
    }
}
