using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using static DeckAdvisor.DeckAdvisorCode.CardAttributeExtractor;

namespace DeckAdvisor.DeckAdvisorCode;

/// <summary>
/// 统一评分公式：
///   score = rawValue / max(cost, 1) × K + (0费牌额外 +2.0)
///
/// 基准：1费6伤 = 1费5格挡 = 5分
///   每点伤害价值 = 5/6 ≈ 0.833
///   每点格挡价值 = 1.0（5格挡=5分）
///
/// 联动规则（来自 score-rule 第12条）：
///   失血牌：有[撕裂/狱火/扯碎]时，失血从惩罚变奖励
///   消耗牌：有[灰烬打击/无惧疼痛]时，消耗从惩罚变奖励
///   多段牌：有力量/易伤来源时，伤害×1.1
/// </summary>
public static class CardBaseScorer
{
    const float K = 1.0f;

    // ── 每点属性的价值 ────────────────────────────────────────────────────
    const float DmgPer   = 5f / 6f;  // 每点伤害（基准：6伤=5分）
    const float BlkPer   = 1.0f;     // 每点格挡（基准：5格挡=5分）
    const float DrawPer  = 2.0f;     // 每张抽牌
    const float EnergyPer= 3.0f;     // 每点费用
    const float ExhPer   = -0.5f;    // 消耗1张（默认惩罚，有配合时变奖励）
    const float VulnPer  = 1.5f;     // 每层易伤/虚弱
    const float StrPer   = 3.0f;     // 每点力量

    /// <summary>
    /// 覆甲N层的总格挡价值。
    /// 覆甲每回合结束给N格挡，层数每回合-1，总格挡 = N×(N+1)/2。
    /// 乘0.8是因为格挡分散在多回合，有时间滞后性。
    /// </summary>
    static float PlatingScore(int n) => n * (n + 1) / 2f * BlkPer * 0.8f;

    /// <summary>
    /// AOE加成系数。
    /// 牌库已有AOE越多，新AOE的边际价值越低。
    /// </summary>
    static float AoeMult(bool isAoe, int aoeCount) =>
        !isAoe ? 1.0f : aoeCount == 0 ? 1.3f : aoeCount == 1 ? 1.0f : 0.7f;

    /// <summary>
    /// 从 CardModel 自动提取属性后计算分数。
    /// 这是对外的主要接口。
    /// </summary>
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

    /// <summary>
    /// 直接用属性结构体计算分数（可用于测试或手动覆盖）。
    /// </summary>
    public static float Calculate(Attrs a, int aoeCountInDeck,
                                   bool hasRupture, bool hasInferno, bool hasTearAsunder,
                                   bool hasAshenStrike, bool hasFeelNoPain,
                                   bool hasStrengthSource, bool hasVulnSource)
    {
        // 联动：失血惩罚/奖励
        float hpMod = (hasRupture || hasInferno || hasTearAsunder) ? 1.0f : -1.0f;

        // 联动：消耗惩罚/奖励
        float exhMod = (hasAshenStrike || hasFeelNoPain) ? 0.5f : ExhPer;

        // 联动：多段×1.1（有力量或易伤来源时）
        float multiMod = (hasStrengthSource || hasVulnSource) ? 1.1f : 1.0f;

        // 伤害分（多段时乘联动系数）
        float dmgRaw = a.Damage * DmgPer * AoeMult(a.IsAoe, aoeCountInDeck)
                     * (a.HitCount > 1 ? multiMod : 1.0f);

        // 格挡分（覆甲用递减公式，普通格挡直接乘系数）
        float blkRaw = a.PlatingStacks > 0
            ? PlatingScore(a.PlatingStacks)
            : a.Block * BlkPer;

        // 汇总所有属性的原始价值
        float raw = dmgRaw
                  + blkRaw
                  + a.DrawCards   * DrawPer
                  + a.GainEnergy  * EnergyPer
                  + a.ExhaustCount * exhMod          // 消耗：惩罚或奖励
                  + a.HpLoss      * hpMod * BlkPer   // 失血：±1格挡分/点
                  + a.VulnStacks  * VulnPer
                  + a.WeakStacks  * VulnPer
                  + a.StrengthGain * StrPer
                  - (a.IsConditional ? 1.0f : 0f);   // 条件性牌扣分

        // 除以费用，0费牌额外+2（0费本身就是价值）
        return raw / Math.Max(a.Cost, 1) * K + (a.Cost == 0 ? 2.0f : 0f);
    }
}
