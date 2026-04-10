namespace DeckAdvisor.DeckAdvisorCode;

/// <summary>
/// 统一评分公式：score = rawValue / max(cost, 1) × k + zeroCostBonus
/// rawValue = 伤害分 + 格挡分 + 功能分 - 负面分
/// 基准：1费打9 = 5分，k ≈ 0.578
/// 无上下限，允许负分。
/// </summary>
public static class CardBaseScorer
{
    // 基准系数：使得 1费打9 = 5分
    // rawValue(1费打9) = 9 × dmgCoeff = 9 × 0.96 = 8.64
    // score = 8.64 / 1 × k = 5 → k = 0.578
    const float K = 0.578f;

    // ── 原始价值系数 ──────────────────────────────────────────────────────
    const float DmgCoeff   = 0.96f;   // 1点伤害的原始价值
    const float BlockCoeff = 1.25f;   // 1点格挡的原始价值（1费5格挡≈1费打9×0.7）
    const float DrawVal    = 2.5f;    // 抽1张牌
    const float EnergyVal  = 3.0f;    // +1费
    const float ExhaustVal = 1.0f;    // 消耗自身（不占牌库）
    const float BurnVal    = 1.5f;    // 消耗手牌1张（精简卡组）
    const float SearchVal  = 2.0f;    // 检索/调牌序
    const float VulnVal    = 1.0f;    // 施加1层易伤
    const float WeakVal    = 1.0f;    // 施加虚弱
    const float StrVal     = 1.5f;    // +1力量

    // 职业系数：战士攻击牌略高，防御牌略低
    const float AttackMult = 1.1f;
    const float BlockMult  = 0.9f;

    // 多段/AOE加成
    static float MultiHitMult(bool isMultiHit) => isMultiHit ? 1.2f : 1.0f;
    static float AoeMult(bool isAoe, int aoeCount) =>
        !isAoe ? 1.0f : aoeCount == 0 ? 1.3f : aoeCount == 1 ? 1.0f : 0.7f;

    // 负面代价
    static float HpLoss(float hp) => hp <= 1 ? 0.5f : hp <= 2 ? 0.8f : hp <= 3 ? 1.0f : hp <= 6 ? 1.5f : 2.0f;

    // ── 核心计算 ──────────────────────────────────────────────────────────
    static float Score(float rawValue, int cost) =>
        rawValue / Math.Max(cost, 1) * K + (cost == 0 ? 2.0f : 0f);

    static float Dmg(float dmg, bool multiHit, bool aoe, int aoeCount) =>
        dmg * DmgCoeff * AttackMult * MultiHitMult(multiHit) * AoeMult(aoe, aoeCount);

    static float Blk(float block) => block * BlockCoeff * BlockMult;

    // 能力牌：rawValue = 每回合触发价值 × 预期回合数
    static float PowerScore(float perTurnValue, int cost, int turns = 3) =>
        Score(perTurnValue * turns, cost);

    // ── 每张卡的基础分 ────────────────────────────────────────────────────
    public static float Calculate(string cardName, int aoeCountInDeck)
    {
        return cardName switch
        {
            // ── 攻击牌 ────────────────────────────────────────────────────
            "StrikeIronclad" => Score(Dmg(6, false, false, 0), 1),
            "Anger"          => Score(Dmg(6, false, false, 0), 0),
            "Bash"           => Score(Dmg(8, false, false, 0) + VulnVal * 2, 2),
            "Breakthrough"   => Score(Dmg(9, false, true, aoeCountInDeck) - HpLoss(1), 1),
            "Bludgeon"       => Score(Dmg(32, false, false, 0), 3),
            "BodySlam"       => Score(Dmg(10, false, false, 0), 1),   // 伤害=格挡，估算10
            "Bully"          => Score(Dmg(4, false, false, 0), 0),    // 无易伤时只打4
            "Conflagration"  => Score(Dmg(8, false, true, aoeCountInDeck), 1),  // 本回合攻击牌数×2
            "Dismantle"      => Score(Dmg(8, false, false, 0) + VulnVal, 1),    // 有易伤打2次
            "FiendFire"      => Score(Dmg(7 * 3, false, false, 0) + ExhaustVal, 2), // 估算消耗3张×7
            "FightMe"        => Score(Dmg(10, true, false, 0) + StrVal * 2, 2),
            "Grapple"        => Score(Dmg(7, false, false, 0), 1),
            "Headbutt"       => Score(Dmg(9, false, false, 0) + SearchVal, 1),
            "Hemokinesis"    => Score(Dmg(14, false, false, 0) - HpLoss(2), 1),
            "HowlFromBeyond" => Score(Dmg(16, false, true, aoeCountInDeck) + ExhaustVal, 3),
            "IronWave"       => Score(Dmg(5, false, false, 0) + Blk(5), 1),
            "Mangle"         => Score(Dmg(15, false, false, 0), 3),
            "MoltenFist"     => Score(Dmg(10, false, false, 0) + ExhaustVal, 1),
            "PactsEnd"       => Score(Dmg(17, false, true, aoeCountInDeck) - 1.0f, 0), // 需条件
            "PerfectedStrike"=> Score(Dmg(9, false, false, 0), 2),   // 估算6张打击牌
            "Pillage"        => Score(Dmg(6, false, false, 0) + DrawVal, 1),
            "PommelStrike"   => Score(Dmg(9, false, false, 0) + DrawVal, 1),
            "Rampage"        => Score(Dmg(9, false, false, 0), 1),
            "SetupStrike"    => Score(Dmg(7, false, false, 0) + StrVal * 2, 1),
            "Spite"          => Score(Dmg(6, false, false, 0) + DrawVal * 0.5f, 0),
            "Stomp"          => Score(Dmg(12, false, true, aoeCountInDeck), 3),
            "SwordBoomerang" => Score(Dmg(9, true, false, 0) - 0.5f, 1),  // 随机目标
            "TearAsunder"    => Score(Dmg(5 * 3, true, false, 0), 2),     // 估算3段
            "Thrash"         => Score(Dmg(8, true, false, 0) + ExhaustVal, 1),
            "Thunderclap"    => Score(Dmg(4, false, true, aoeCountInDeck) + VulnVal, 1),
            "TwinStrike"     => Score(Dmg(10, true, false, 0), 1),
            "Unrelenting"    => Score(Dmg(12, false, false, 0) + EnergyVal * 0.5f, 2),
            "Uppercut"       => Score(Dmg(13, false, false, 0) + VulnVal + WeakVal, 2),
            "Whirlwind"      => Score(Dmg(5 * 3, true, true, aoeCountInDeck), 1), // 估算3费

            // ── 技能牌 ────────────────────────────────────────────────────
            "DefendIronclad" => Score(Blk(5), 1),
            "ShrugItOff"     => Score(Blk(8) + DrawVal, 1),
            "Armaments"      => Score(Blk(5) + 2.0f, 1),
            "BattleTrance"   => Score(DrawVal * 3, 0),
            "BloodWall"      => Score(Blk(16) - HpLoss(2), 2),
            "Bloodletting"   => Score(EnergyVal * 2 - HpLoss(3), 0),
            "BurningPact"    => Score(BurnVal + DrawVal * 2 + ExhaustVal, 1),
            "Cascade"        => Score(DrawVal * 2, 0),
            "Colossus"       => Score(Blk(5) + 2.0f, 1),
            "DemonicShield"  => Score(Blk(8) + ExhaustVal - HpLoss(1), 0),
            "Dominate"       => Score(StrVal * 3 + ExhaustVal, 1),
            "EvilEye"        => Score(Blk(8), 1),
            "ExpectAFight"   => Score(EnergyVal * 2, 2),
            "FeelNoPain"     => Score(Blk(3) * 3, 1),   // 估算3次消耗
            "FlameBarrier"   => Score(Blk(12), 2),
            "ForgottenRitual"=> Score(EnergyVal * 3 - 1.0f, 1),
            "Havoc"          => Score(BurnVal * 0.5f, 1),
            "Impervious"     => Score(Blk(30) + ExhaustVal, 2),
            "InfernalBlade"  => Score(Dmg(8, false, false, 0) + ExhaustVal, 1),
            "Offering"       => Score(EnergyVal * 2 + DrawVal * 3 - HpLoss(6) + ExhaustVal, 0),
            "PrimalForce"    => Score(Dmg(20, false, false, 0) * 0.7f, 0), // 条件性，打折
            "SecondWind"     => Score(BurnVal * 2 + Blk(10), 1),
            "Stoke"          => Score(BurnVal * 3 + DrawVal * 3 + ExhaustVal, 1),
            "TrueGrit"       => Score(Blk(7) + BurnVal * 0.5f, 1),
            "Unmovable"      => Score(Blk(7) * 2, 2),  // 翻倍格挡，估算
            "Brand"          => Score(BurnVal + StrVal - HpLoss(1), 0),
            "Juggling"       => Score(DrawVal * 2, 1),
            "Tremble"        => Score(VulnVal * 2, 1),

            // ── 能力牌 ────────────────────────────────────────────────────
            "Aggression"     => PowerScore(DrawVal, 1),
            "Barricade"      => Score(Blk(10) * 3, 3),  // 格挡永久保留，极高价值
            "CrimsonMantle"  => PowerScore(Blk(8) - HpLoss(1), 1),
            "Corruption"     => Score(EnergyVal * 5, 3),  // 所有技能0费，极强
            "Cruelty"        => PowerScore(VulnVal * 2, 1),
            "DarkEmbrace"    => PowerScore(DrawVal, 2),
            "DemonForm"      => PowerScore(StrVal * 2, 3),
            "DrumOfBattle"   => PowerScore(DrawVal + BurnVal * 0.3f, 0),
            "Hellraiser"     => PowerScore(BurnVal, 2),
            "Inflame"        => PowerScore(DrawVal * 2, 1),
            "Inferno"        => PowerScore(Dmg(4, false, true, aoeCountInDeck) - HpLoss(1), 1),
            "Juggernaut"     => PowerScore(Dmg(5, false, false, 0) * 0.5f, 2),
            "OneTwoPunch"    => PowerScore(DrawVal, 1),
            "Pyre"           => PowerScore(EnergyVal, 2),
            "Rage"           => PowerScore(Blk(3) * 2, 0),  // 每次攻击+3格挡，估算2次/回合
            "Rupture"        => PowerScore(StrVal, 1),
            "Stampede"       => PowerScore(Dmg(7, false, false, 0) * 0.5f, 2),
            "StoneArmor"     => Score(Blk(4 + 3 + 2 + 1), 1),  // 护甲递减：4+3+2+1=10总格挡
            "Tank"           => PowerScore(Blk(3), 1),
            "Vicious"        => PowerScore(DrawVal, 1),

            _ => 3.0f
        };
    }
}
