using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace DeckAdvisor.DeckAdvisorCode;

/// <summary>
/// 从 CardModel.DynamicVars 自动提取评分所需的属性值。
///
/// 动态/条件性属性无法在选牌时计算，使用以下默认值兜底：
///   依赖格挡值  → 5
///   依赖伤害值  → 6
///   依赖费用数  → 3
///   依赖手牌数  → 5
/// </summary>
public static class CardAttributeExtractor
{
    // 兜底默认值
    const float DefaultBlock  = 5f;
    const float DefaultDamage = 6f;
    const float DefaultEnergy = 3f;
    const float DefaultHand   = 5f;

    /// <summary>
    /// 卡牌属性集合，作为评分计算的输入。
    /// 所有字段均为非负数（负面效果通过 HpLoss 等单独字段表示）。
    /// </summary>
    public record Attrs(
        int   Cost,           // 费用
        float Damage,         // 单次伤害
        int   HitCount,       // 段数（1=单段，>1=多段）
        bool  IsAoe,          // 是否群体攻击
        float Block,          // 格挡值（含覆甲总量）
        int   DrawCards,      // 抽牌数
        int   GainEnergy,     // 获得费用
        int   ExhaustCount,   // 消耗张数（含消耗自身）
        float HpLoss,         // 失去生命值
        float VulnStacks,     // 施加易伤层数
        float WeakStacks,     // 施加虚弱层数
        float StrengthGain,   // 获得力量
        int   PlatingStacks,  // 覆甲层数（递减式格挡）
        bool  IsConditional   // 是否需要满足条件才能打出
    );

    /// <summary>
    /// 从 CardModel 提取属性。
    /// 遍历 DynamicVars，按类型识别各属性值。
    /// </summary>
    public static Attrs Extract(CardModel card)
    {
        var vars = card.DynamicVars;
        int cost = card.EnergyCost.Canonical;

        float damage    = 0f;
        int   hitCount  = 1;
        bool  isAoe     = card.TargetType == TargetType.AllEnemies;
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

        // 消耗自身关键字
        if (card.Keywords.Contains(CardKeyword.Exhaust)) exhaust += 1;

        // 遍历 DynamicVars，按子类型识别属性
        foreach (var v in vars.Values)
        {
            switch (v)
            {
                case DamageVar dv:
                    damage = (float)dv.BaseValue;
                    break;
                case CalculatedDamageVar:
                    // 动态伤害（如全身撞击=当前格挡，焚烧=攻击牌数×2）→ 用默认值兜底
                    damage = DefaultDamage;
                    break;
                case BlockVar bv:
                    block = (float)bv.BaseValue;
                    break;
                case CalculatedBlockVar:
                    // 动态格挡（如恶魔护盾=当前格挡）→ 用默认值兜底
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
                    // 覆甲：递减式格挡，在 CardBaseScorer 中用 N×(N+1)/2×0.8 计算
                    plating = (int)pv.BaseValue;
                    break;
            }
        }

        // 段数：WithHitCount() 在 OnPlay 里设置，无法从 DynamicVars 读取，用已知列表补充
        hitCount = KnownHitCounts.TryGetValue(card.GetType().Name, out var hc) ? hc : 1;

        // 条件性牌：需要满足特定条件才能打出（如契约终结需消耗堆≥3张）
        conditional = KnownConditional.Contains(card.GetType().Name);

        return new Attrs(cost, damage, hitCount, isAoe, block, draw, energy,
                         exhaust, hpLoss, vuln, weak, strength, plating, conditional);
    }

    /// <summary>
    /// 已知多段牌的段数。
    /// WithHitCount() 在运行时设置，无法从 DynamicVars 静态读取，需手动维护。
    /// </summary>
    static readonly Dictionary<string, int> KnownHitCounts = new()
    {
        { "TwinStrike",    2 },
        { "SwordBoomerang",3 },
        { "Thrash",        2 },
        { "TearAsunder",   3 },  // 段数=失血次数，估算3段
        { "Whirlwind",     3 },  // X费，估算3费×1段
        { "FightMe",       2 },
        { "Dismantle",     2 },  // 有易伤时打2次，默认按2算
    };

    /// <summary>需要满足条件才能打出的牌。</summary>
    static readonly HashSet<string> KnownConditional = new()
        { "PactsEnd" };  // 契约终结：消耗堆≥3张才可打
}
