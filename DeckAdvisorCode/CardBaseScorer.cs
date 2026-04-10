using MegaCrit.Sts2.Core.Entities.Cards;

namespace DeckAdvisor.DeckAdvisorCode;

/// <summary>
/// 基于费效比和功能价值计算卡牌基础分。
/// 基准：1费打9伤=5分，功能价值叠加，负面效果减分。
/// </summary>
public static class CardBaseScorer
{
    // ── 伤害基准（1费打9=5分） ────────────────────────────────────────────
    // 每费用对应的基准伤害和每点伤害的分值
    static float DamageScore(float damage, int cost, bool isMultiHit, bool isAoe,
                              int aoeCountInDeck)
    {
        float baseDmg = cost switch { 0 => 4.5f, 1 => 9f, 2 => 18f, 3 => 27f, _ => 9f };
        float perDmg  = cost switch { 0 => 0.55f, 1 => 0.28f, 2 => 0.14f, 3 => 0.09f, _ => 0.28f };

        float score = 5f + (damage - baseDmg) * perDmg;

        if (isMultiHit) score *= 1.2f;  // 多段独立结算力量/易伤
        if (isAoe)
        {
            float aoeMult = aoeCountInDeck == 0 ? 1.3f : aoeCountInDeck == 1 ? 1.0f : 0.7f;
            score *= aoeMult;
        }

        return Math.Max(score, 0f);
    }

    // ── 功能价值 ─────────────────────────────────────────────────────────
    static float DrawScore(int cards) => cards switch { 1 => 1.5f, 2 => 2.5f, 3 => 3.5f, _ => 3.5f + (cards - 3) * 0.8f };
    static float EnergyScore(int energy) => energy switch { 1 => 3.0f, 2 => 5.0f, 3 => 7.0f, _ => 7.0f };
    static float ExhaustSelfScore() => 1.0f;
    static float ExhaustHandScore(int cards) => cards * 1.5f;
    static float SearchScore() => 1.5f;
    // 格挡分：按费用基准，1费5格挡=5分
    static float BlockScore(float block, int cost = 1) => cost switch
    {
        0 => block * 2.0f,
        1 => block * 1.0f,
        2 => block * 0.5f,
        _ => block * 0.33f,
    };
    static float VulnerableScore(int stacks) => stacks == 1 ? 1.0f : 1.8f;
    static float WeakScore() => 1.0f;
    static float StrengthScore(int amount) => amount == 1 ? 1.5f : 2.5f;

    static float PowerCostPenalty(int cost) => cost switch
    {
        0 => 1.0f,
        1 => 0f,
        2 => -1.5f,
        _ => -3.0f,
    };
    static float HpLossCost(float hp) => hp switch { <= 1 => 0.5f, <= 2 => 0.8f, <= 3 => 1.0f, <= 6 => 1.5f, _ => 2.0f };
    static float ConditionCost() => 1.0f;   // 需要满足条件才能打出
    static float RandomTargetCost() => 0.5f;

    // ── 每张卡的基础分 ───────────────────────────────────────────────────
    public static float Calculate(string cardName, int aoeCountInDeck)
    {
        float result = cardName switch
        {
            // ── 攻击牌 ──────────────────────────────────────────────────
            "StrikeIronclad" => DamageScore(6, 1, false, false, 0),                          // 1费打6，基础牌
            "Anger"          => DamageScore(6, 0, false, false, 0),                          // 0费打6
            "Bash"           => DamageScore(8, 2, false, false, 0) + VulnerableScore(2),     // 2费打8+2易伤
            "Breakthrough"   => DamageScore(9, 1, false, true, aoeCountInDeck) - HpLossCost(1), // 1费AOE9+失1血
            "Bludgeon"       => DamageScore(32, 3, false, false, 0),                         // 3费打32
            "BodySlam"       => 5.0f,  // 伤害=当前格挡，动态，给中等基础分
            "Bully"          => 3.0f,  // 0费打4+易伤层数，无易伤时很弱
            "Conflagration"  => DamageScore(8, 1, false, true, aoeCountInDeck),              // 1费AOE8+本回合攻击牌数×2
            "Dismantle"      => DamageScore(8, 1, false, false, 0) + VulnerableScore(1),     // 1费打8，有易伤打2次
            "FiendFire"      => 6.0f + ExhaustSelfScore(),  // 2费消耗所有手牌×7伤，动态
            "FightMe"        => DamageScore(10, 2, true, false, 0) + StrengthScore(2),       // 2费打5×2+2力量
            "Grapple"        => DamageScore(7, 1, false, false, 0),                          // 1费打7+控制
            "Headbutt"       => DamageScore(9, 1, false, false, 0) + SearchScore(),          // 1费打9+检索
            "Hemokinesis"    => DamageScore(14, 1, false, false, 0) - HpLossCost(2),         // 1费打14-失2血
            "HowlFromBeyond" => DamageScore(16, 3, false, true, aoeCountInDeck) + ExhaustSelfScore(), // 3费AOE16消耗
            "IronWave"       => DamageScore(5, 1, false, false, 0) + BlockScore(5, 1),          // 1费打5+5格挡
            "Mangle"         => DamageScore(15, 3, false, false, 0),                         // 3费打15+敌人-10力量
            "MoltenFist"     => DamageScore(10, 1, false, false, 0) + ExhaustSelfScore(),    // 1费打10消耗+翻倍易伤
            "PactsEnd"       => DamageScore(17, 0, false, true, aoeCountInDeck) - ConditionCost(), // 0费AOE17需消耗堆≥3
            "PerfectedStrike"=> 5.0f,  // 2费，伤害随打击牌数量变化，动态
            "Pillage"        => DamageScore(6, 1, false, false, 0) + DrawScore(1),           // 1费打6+持续抽攻击牌
            "PommelStrike"   => DamageScore(9, 1, false, false, 0) + DrawScore(1),           // 1费打9+抽1
            "Rampage"        => DamageScore(9, 1, false, false, 0),                          // 1费打9，每次打出+3最大血
            "SetupStrike"    => DamageScore(7, 1, false, false, 0) + StrengthScore(2),       // 1费打7+2力量（临时）
            "Spite"          => DamageScore(6, 0, false, false, 0) + DrawScore(1) * 0.5f,    // 0费打6，受伤时抽1（条件性）
            "Stomp"          => DamageScore(12, 3, false, true, aoeCountInDeck),             // 3费AOE12+本回合攻击牌数加成
            "SwordBoomerang" => DamageScore(9, 1, true, false, 0) - RandomTargetCost(),      // 1费3×3多段随机
            "TearAsunder"    => 6.0f,  // 2费，段数=失血次数，动态，给中等基础分
            "Thrash"         => DamageScore(8, 1, true, false, 0) + ExhaustSelfScore(),      // 1费打4×2消耗
            "Thunderclap"    => DamageScore(4, 1, false, true, aoeCountInDeck) + VulnerableScore(1), // 1费AOE4+1易伤
            "TwinStrike"     => DamageScore(10, 1, true, false, 0),                          // 1费打5×2多段
            "Unrelenting"    => DamageScore(12, 2, false, false, 0),                         // 2费打12+下张攻击0费
            "Uppercut"       => DamageScore(13, 2, false, false, 0) + VulnerableScore(1) + WeakScore(), // 2费打13+虚弱+易伤
            "Whirlwind"      => DamageScore(5, 1, true, true, aoeCountInDeck),               // X费AOE多段，按1费算

            // ── 技能牌 ──────────────────────────────────────────────────
            "DefendIronclad" => BlockScore(5, 1),
            "ShrugItOff"     => BlockScore(8, 1) + DrawScore(1),
            "Armaments"      => BlockScore(5, 1) + 2.0f,
            "BattleTrance"   => DrawScore(3),                                               // 0费抽3（本回合不能再抽）
            "BloodWall"      => BlockScore(16, 2) - HpLossCost(2),
            "Bloodletting"   => EnergyScore(2) - HpLossCost(3),                             // 0费+2费-失3血
            "BurningPact"    => ExhaustHandScore(1) + DrawScore(2) + ExhaustSelfScore(),
            "Cascade"        => 4.0f,
            "Colossus"       => BlockScore(5, 1) + 2.0f,
            "DemonicShield"  => 4.0f + ExhaustSelfScore() - HpLossCost(1),
            "Dominate"       => StrengthScore(2) + ExhaustSelfScore(),
            "EvilEye"        => BlockScore(8, 1),
            "ExpectAFight"   => EnergyScore(1) * 0.8f,
            "FeelNoPain"     => 3.0f,
            "FlameBarrier"   => BlockScore(12, 2),
            "ForgottenRitual"=> EnergyScore(3) - ConditionCost(),
            "Havoc"          => ExhaustHandScore(1) * 0.5f,
            "Impervious"     => BlockScore(30, 2) + ExhaustSelfScore(),
            "InfernalBlade"  => 3.5f + ExhaustSelfScore(),
            "Offering"       => EnergyScore(2) + DrawScore(3) - HpLossCost(6) + ExhaustSelfScore(),
            "PrimalForce"    => 5.0f,
            "SecondWind"     => ExhaustHandScore(2) + BlockScore(10, 1),
            "Stoke"          => ExhaustHandScore(3) + DrawScore(3) + ExhaustSelfScore(),
            "TrueGrit"       => BlockScore(7, 1) + ExhaustHandScore(1) * 0.5f,
            "Unmovable"      => 4.0f,
            "Brand"          => ExhaustHandScore(1) + StrengthScore(1) - HpLossCost(1),
            "Juggling"       => 3.5f,
            "Tremble"        => VulnerableScore(2),

            // ── 能力牌（价值=预期触发价值，费用越高要求越高）──────────────
            // 基准：N费能力牌需要产生 N×5 分的总价值才算合格
            // 每回合触发价值 × 预期触发回合数（约3回合）- 费用标准
            "Aggression"     => DrawScore(1) * 3f - 5f,          // 1费，每回合抽1攻击牌，3回合=4.5，-5=-0.5→给3分保底
            "Barricade"      => 8.0f,                             // 3费，格挡永久保留，配合全身撞击价值极高
            "CrimsonMantle"  => (BlockScore(8, 1) - HpLossCost(1)) * 3f - 5f, // 1费，每回合(8-0.5)×3=22.5-5=17.5
            "Corruption"     => 10.0f,                            // 3费，所有技能牌0费消耗，极强
            "Cruelty"        => VulnerableScore(1) * 3f - 5f,    // 1费，易伤时+25%伤害，需配合
            "DarkEmbrace"    => DrawScore(1) * 3f - 10f,         // 2费，每次消耗抽1，3次=4.5-10=-5.5→给3分保底
            "DemonForm"      => StrengthScore(2) * 3f - 15f,     // 3费，每回合+2力量，3回合=7.5-15=-7.5→给2分保底
            "DrumOfBattle"   => DrawScore(1) * 3f - 0f,          // 0费，每回合烧顶+抽1，3回合=4.5
            "Hellraiser"     => ExhaustHandScore(1) * 3f - 10f,  // 2费，每次攻击消耗顶牌，3次=4.5-10=-5.5→给2分保底
            "Inflame"        => DrawScore(1) * 4f - 5f,          // 1费，每次给易伤抽牌，4次=6-5=1→给4分保底
            "Inferno"        => (4.0f - HpLossCost(1)) * 3f - 5f, // 1费，每回合失血+AOE，3回合=10.5-5=5.5
            "Juggernaut"     => 3.0f,                             // 2费，获得格挡时随机伤害，条件性
            "OneTwoPunch"    => DrawScore(1) * 3f - 5f,          // 1费，每回合复制攻击牌，3回合=4.5-5=-0.5→给3分保底
            "Pyre"           => EnergyScore(1) * 3f - 10f,       // 2费，每回合+1费，3回合=9-10=-1→给4分保底
            "Rage"           => BlockScore(3, 1) * 4f - 0f,      // 0费，每次攻击+3格挡，4次=12
            "Rupture"        => StrengthScore(1) * 4f - 5f,      // 1费，受伤时+1力量，4次=6-5=1→给4分保底
            "Stampede"       => 4.0f,                             // 2费，每回合结束随机打出攻击牌
            "StoneArmor"     => BlockScore(4, 1) * 3f - 5f,      // 1费，每回合+4护甲，3回合=12-5=7
            "Tank"           => BlockScore(3, 1) * 3f - 5f,      // 1费，每回合+格挡，3回合=9-5=4
            "Vicious"        => DrawScore(1) * 3f - 5f,          // 1费，给易伤时抽牌，3次=4.5-5=-0.5→给3分保底

            _ => 3.0f  // 未知卡默认分
        };
        return Math.Max(result, 1.0f);  // 保底1分
    }
}
