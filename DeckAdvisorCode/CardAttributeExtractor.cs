using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace DeckAdvisor.DeckAdvisorCode;

/// <summary>
/// 从 CardModel.DynamicVars 自动提取评分所需属性。
/// 动态/条件性属性用规则默认值兜底：
///   依赖格挡=5, 依赖伤害=6, 依赖费用=3, 依赖手牌=5
/// </summary>
public static class CardAttributeExtractor
{
    const float DefaultBlock  = 5f;
    const float DefaultDamage = 6f;
    const float DefaultEnergy = 3f;
    const float DefaultHand   = 5f;

    public record Attrs(
        int   Cost,
        float Damage,
        int   HitCount,
        bool  IsAoe,
        float Block,
        int   DrawCards,
        int   GainEnergy,
        int   ExhaustCount,   // 消耗张数（含自身）
        float HpLoss,
        float VulnStacks,
        float WeakStacks,
        float StrengthGain,
        int   PlatingStacks,
        bool  IsConditional
    );

    public static Attrs Extract(CardModel card)
    {
        var vars = card.DynamicVars;
        int cost = card.EnergyCost.Canonical;

        float damage    = 0f;
        int   hitCount  = 1;
        bool  isAoe     = card.TargetType == MegaCrit.Sts2.Core.Entities.Cards.TargetType.AllEnemies;
        float block     = 0f;
        int   draw      = 0;
        int   energy    = 0;
        int   exhaust   = 0;
        float hpLoss    = 0f;
        float vuln      = 0f;
        float weak      = 0f;
        float strength  = 0f;
        int   plating   = 0;
        bool  conditional = false;

        // 消耗自身
        if (card.Keywords.Contains(CardKeyword.Exhaust)) exhaust += 1;

        foreach (var v in vars.Values)
        {
            switch (v)
            {
                case DamageVar dv:
                    damage = (float)dv.BaseValue;
                    break;
                case CalculatedDamageVar cdv:
                    // 动态伤害：用默认值兜底
                    damage = DefaultDamage;
                    break;
                case BlockVar bv:
                    block = (float)bv.BaseValue;
                    break;
                case CalculatedBlockVar cbv:
                    block = DefaultBlock;
                    break;
                case HpLossVar hv:
                    hpLoss = (float)hv.BaseValue;
                    break;
                case CardsVar cv:
                    draw = (int)cv.BaseValue;
                    break;
                case EnergyVar ev:
                    energy = (int)ev.BaseValue;
                    break;
                case PowerVar<VulnerablePower> vv:
                    vuln = (float)vv.BaseValue;
                    break;
                case PowerVar<WeakPower> wv:
                    weak = (float)wv.BaseValue;
                    break;
                case PowerVar<StrengthPower> sv:
                    strength = (float)sv.BaseValue;
                    break;
                case PowerVar<PlatingPower> pv:
                    plating = (int)pv.BaseValue;
                    break;
            }
        }

        // 段数：从 ExtraDamageVar 或 CalculatedVar 推断不可靠，
        // 已知多段牌通过 WithHitCount 设置，这里用关键字检测
        // 已知多段牌列表（无法从 DynamicVars 自动获取）
        hitCount = KnownHitCounts.TryGetValue(card.GetType().Name, out var hc) ? hc : 1;

        // 条件性：需要消耗堆/手牌条件才能打出
        conditional = KnownConditional.Contains(card.GetType().Name);

        return new Attrs(cost, damage, hitCount, isAoe, block, draw, energy,
                         exhaust, hpLoss, vuln, weak, strength, plating, conditional);
    }

    // 已知多段牌的段数（无法从 DynamicVars 自动获取）
    static readonly Dictionary<string, int> KnownHitCounts = new()
    {
        { "TwinStrike",    2 },
        { "SwordBoomerang",3 },
        { "Thrash",        2 },
        { "TearAsunder",   3 },  // 估算3段
        { "Whirlwind",     3 },  // 估算3费×1段
        { "FightMe",       2 },
        { "Dismantle",     2 },  // 有易伤时打2次，默认按2算
    };

    // 需要条件才能打出的牌
    static readonly HashSet<string> KnownConditional = new()
        { "PactsEnd" };
}
