using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace DeckAdvisor.DeckAdvisorCode;

// 每次进入战斗时清空评分缓存，避免使用上一场战斗的过期数据
[HarmonyPatch(typeof(NCombatRoom), "_Ready")]
public static class CombatRoomPatch
{
    static void Postfix() => CardScorer.Current.Clear();
}
