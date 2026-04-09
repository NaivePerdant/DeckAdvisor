using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace DeckAdvisor.DeckAdvisorCode;

/// <summary>
/// Intercepts the card reward screen just before it's shown to compute scores.
/// The cards passed here are already the final reward options (after all relic modifications).
/// </summary>
[HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromChooseACardScreen))]
public static class CardRewardScoringPatch
{
    static void Prefix(PlayerChoiceContext context, IReadOnlyList<CardModel> cards, Player player)
    {
        CardScorer.Evaluate(player, cards);
    }
}
