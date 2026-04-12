using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using static DeckAdvisor.DeckAdvisorCode.CardAttributeExtractor;

namespace DeckAdvisor.DeckAdvisorCode;

/// <summary>
/// 统一评分公式：score = rawValue / max(cost,1) × K + (0费 +2.0)
/// 基准：1费6伤 = 1费5格挡 = 5分
/// </summary>
public static class CardBaseScorer
{
    const float K = 1.0f;

    // 每点价值
    const float DmgPer   = 5f / 6f;  // 6伤=5分
    const float BlkPer   = 1.0f;     // 5格挡=5分
    const float DrawPer  = 2.0f;
    const float EnergyPer= 3.0f;
    const float ExhPer   = -0.5f;    // 消耗1张（惩罚）
    const float VulnPer  = 1.5f;
    const float StrPer   = 3.0f;

    // 覆甲N层：总格挡 = N×(N+1)/2，乘0.8（回合滞后性）
    static float PlatingScore(int n) => n * (n + 1) / 2f * BlkPer * 0.8f;

    // AOE加成
    static float AoeMult(bool isAoe, int aoeCount) =>
        !isAoe ? 1.0f : aoeCount == 0 ? 1.3f : aoeCount == 1 ? 1.0f : 0.7f;

    public static float Calculate(CardModel card, int aoeCountInDeck,
                                   bool hasRupture, bool hasInferno, bool hasTearAsunder,
                                   bool hasAshenStrike, bool hasFeelNoPain,
                                   bool hasStrengthSource, bool hasVulnSource)
    {
        var a = CardAttributeExtractor.Extract(card);
        return Calculate(a, aoeCountInDeck,
            hasRupture, hasInferno, hasTearAsunder,
            hasAshenStrike, hasFeelNoPain,
            hasStrengthSource, hasVulnSource);
    }

    public static float Calculate(Attrs a, int aoeCountInDeck,
                                   bool hasRupture, bool hasInferno, bool hasTearAsunder,
                                   bool hasAshenStrike, bool hasFeelNoPain,
                                   bool hasStrengthSource, bool hasVulnSource)
    {
        // 失血：有撕裂/狱火/扯碎时变奖励
        float hpMod = (hasRupture || hasInferno || hasTearAsunder) ? 1.0f : -1.0f;
        // 消耗：有灰烬打击/无惧疼痛时变奖励
        float exhMod = (hasAshenStrike || hasFeelNoPain) ? 0.5f : ExhPer;
        // 多段×1.1：有力量/易伤来源时
        float multiMod = (hasStrengthSource || hasVulnSource) ? 1.1f : 1.0f;

        float dmgRaw = a.Damage * DmgPer * AoeMult(a.IsAoe, aoeCountInDeck)
                     * (a.HitCount > 1 ? multiMod : 1.0f);
        float blkRaw = a.PlatingStacks > 0
            ? PlatingScore(a.PlatingStacks)
            : a.Block * BlkPer;

        float raw = dmgRaw
                  + blkRaw
                  + a.DrawCards  * DrawPer
                  + a.GainEnergy * EnergyPer
                  + a.ExhaustCount * exhMod
                  + a.HpLoss     * hpMod * BlkPer   // 失1血 = ±1格挡分
                  + a.VulnStacks * VulnPer
                  + a.WeakStacks * VulnPer
                  + a.StrengthGain * StrPer
                  - (a.IsConditional ? 1.0f : 0f);

        return raw / Math.Max(a.Cost, 1) * K + (a.Cost == 0 ? 2.0f : 0f);
    }
}
