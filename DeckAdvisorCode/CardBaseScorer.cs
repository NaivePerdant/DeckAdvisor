namespace DeckAdvisor.DeckAdvisorCode;

/// <summary>
/// 评分公式：score = rawValue / max(cost, 1) × K + (0费牌 +2.0)
/// 基准：1费6伤 = 1费5格挡 = 5分
/// 每点伤害/格挡 = 5/6 ≈ 0.833，K = 5 / (6 × 0.833) = 1.0
/// </summary>
public static class CardBaseScorer
{
    const float K = 1.0f;

    // ── 价值系数 ──────────────────────────────────────────────────────────
    const float DmgPer   = 5f / 6f;   // 每点伤害价值（基准：6伤=5分）
    const float BlkPer   = 5f / 6f;   // 每点格挡价值（基准：5格挡=5分，但5×(5/6)≈4.17，调整为1.0使5格挡=5分）
    // 重新推导：1费5格挡=5分 → 5×BlkPer/1×K=5 → BlkPer=1.0
    // 1费6伤=5分 → 6×DmgPer/1×K=5 → DmgPer=5/6≈0.833
    // 但要求"6点伤害和5点格挡分数相同"：6×DmgPer = 5×BlkPer
    // 设 BlkPer=1.0，则 DmgPer = 5/6 ≈ 0.833 ✓

    const float DrawPer  = 2.0f;   // 抽1张牌
    const float EnergyPer= 3.0f;   // +1费
    const float ExhPer   = -0.5f;  // 消耗1张（含自身）
    const float VulnPer  = 1.5f;   // 易伤/虚弱每层
    const float StrPer   = 3.0f;   // +1力量

    // 失1血 = -1格挡分 = -1.0
    static float HpLoss(float hp) => -hp * 1.0f;

    // AOE加成（逻辑不变）
    static float AoeMult(bool isAoe, int aoeCount) =>
        !isAoe ? 1.0f : aoeCount == 0 ? 1.3f : aoeCount == 1 ? 1.0f : 0.7f;

    // 核心计算
    static float Score(float rawValue, int cost) =>
        rawValue / Math.Max(cost, 1) * K + (cost == 0 ? 2.0f : 0f);

    static float Dmg(float dmg, bool aoe, int aoeCount) =>
        dmg * DmgPer * AoeMult(aoe, aoeCount);

    static float Blk(float block) => block * 1.0f;  // BlkPer=1.0

    // ── 每张卡的基础分 ────────────────────────────────────────────────────
    public static float Calculate(string cardName, int aoeCountInDeck,
                                   bool hasRupture, bool hasInferno, bool hasTearAsunder,
                                   bool hasAshenStrike, bool hasFeelNoPain,
                                   bool hasStrengthSource, bool hasVulnSource)
    {
        // 失血惩罚/奖励：有撕裂/狱火/扯碎时变奖励
        float hpMod = (hasRupture || hasInferno || hasTearAsunder) ? 1.0f : -1.0f;
        float HpEffect(float hp) => hp * hpMod;  // 正=奖励，负=惩罚

        // 消耗惩罚/奖励：有灰烬打击/无惧疼痛时变奖励
        float exhMod = (hasAshenStrike || hasFeelNoPain) ? 0.5f : ExhPer;
        float Exh(int n = 1) => n * exhMod;

        // 多段×1.1：有力量/活力/易伤来源时
        float multiMod = (hasStrengthSource || hasVulnSource) ? 1.1f : 1.0f;
        float MultiDmg(float dmg, bool aoe, int aoeCount) =>
            Dmg(dmg, aoe, aoeCount) * multiMod;

        return cardName switch
        {
            // ── 攻击牌 ────────────────────────────────────────────────────
            "StrikeIronclad" => Score(Dmg(6, false, 0), 1),
            "Anger"          => Score(Dmg(6, false, 0), 0),
            "Bash"           => Score(Dmg(8, false, 0) + VulnPer * 2, 2),
            "Breakthrough"   => Score(Dmg(9, true, aoeCountInDeck) + HpEffect(1), 1),
            "Bludgeon"       => Score(Dmg(32, false, 0), 3),
            "BodySlam"       => Score(Dmg(10, false, 0), 1),   // 伤害=格挡，估算10
            "Bully"          => Score(Dmg(4, false, 0), 0),    // 无易伤时只打4
            "Conflagration"  => Score(Dmg(8, true, aoeCountInDeck), 1),
            "Dismantle"      => Score(Dmg(8, false, 0) + VulnPer, 1),
            "FiendFire"      => Score(Dmg(7 * 5, false, 0) + Exh(5), 2), // 估算消耗5张手牌
            "FightMe"        => Score(MultiDmg(10, false, 0) + StrPer * 2, 2),
            "Grapple"        => Score(Dmg(7, false, 0), 1),
            "Headbutt"       => Score(Dmg(9, false, 0) + DrawPer, 1),
            "Hemokinesis"    => Score(Dmg(14, false, 0) + HpEffect(2), 1),
            "HowlFromBeyond" => Score(Dmg(16, true, aoeCountInDeck) + Exh(), 3),
            "IronWave"       => Score(Dmg(5, false, 0) + Blk(5), 1),
            "Mangle"         => Score(Dmg(15, false, 0), 3),
            "MoltenFist"     => Score(Dmg(10, false, 0) + Exh(), 1),
            "PactsEnd"       => Score(Dmg(17, true, aoeCountInDeck) - 1.0f, 0),
            "PerfectedStrike"=> Score(Dmg(9, false, 0), 2),
            "Pillage"        => Score(Dmg(6, false, 0) + DrawPer, 1),
            "PommelStrike"   => Score(Dmg(9, false, 0) + DrawPer, 1),
            "Rampage"        => Score(Dmg(9, false, 0), 1),
            "SetupStrike"    => Score(Dmg(7, false, 0) + StrPer * 2, 1),
            "Spite"          => Score(Dmg(6, false, 0) + DrawPer * 0.5f, 0),
            "Stomp"          => Score(Dmg(12, true, aoeCountInDeck), 3),
            "SwordBoomerang" => Score(MultiDmg(9, false, 0) - 0.5f, 1),
            "TearAsunder"    => Score(MultiDmg(5 * 3, false, 0), 2),
            "Thrash"         => Score(MultiDmg(8, false, 0) + Exh(), 1),
            "Thunderclap"    => Score(Dmg(4, true, aoeCountInDeck) + VulnPer, 1),
            "TwinStrike"     => Score(MultiDmg(10, false, 0), 1),
            "Unrelenting"    => Score(Dmg(12, false, 0) + EnergyPer * 0.5f, 2),
            "Uppercut"       => Score(Dmg(13, false, 0) + VulnPer * 2, 2),
            "Whirlwind"      => Score(MultiDmg(5 * 3, true, aoeCountInDeck), 1),

            // ── 技能牌 ────────────────────────────────────────────────────
            "DefendIronclad" => Score(Blk(5), 1),
            "ShrugItOff"     => Score(Blk(8) + DrawPer, 1),
            "Armaments"      => Score(Blk(5) + DrawPer, 1),   // 升级手牌≈抽1张价值
            "BattleTrance"   => Score(DrawPer * 3, 0),
            "BloodWall"      => Score(Blk(16) + HpEffect(2), 2),
            "Bloodletting"   => Score(EnergyPer * 2 + HpEffect(3), 0),
            "BurningPact"    => Score(Exh() + DrawPer * 2, 1),
            "Cascade"        => Score(DrawPer * 5, 0),         // 打出牌堆顶X张，按手牌5张算
            "Colossus"       => Score(Blk(5) + VulnPer * 2, 1),
            "DemonicShield"  => Score(Blk(8) + Exh() + HpEffect(1), 0),
            "Dominate"       => Score(StrPer * 3 + Exh(), 1), // 易伤层数转力量，估算3层
            "EvilEye"        => Score(Blk(8), 1),
            "ExpectAFight"   => Score(EnergyPer * 3, 2),       // 手牌攻击牌按3算
            "FeelNoPain"     => Score(Blk(3) * 3, 1),
            "FlameBarrier"   => Score(Blk(12), 2),
            "ForgottenRitual"=> Score(EnergyPer * 3 - 1.0f, 1),
            "Havoc"          => Score(Exh(), 1),
            "Impervious"     => Score(Blk(30) + Exh(), 2),
            "InfernalBlade"  => Score(Dmg(8, false, 0) + Exh(), 1),
            "Offering"       => Score(EnergyPer * 2 + DrawPer * 3 + HpEffect(6) + Exh(), 0),
            "PrimalForce"    => Score(Dmg(20, false, 0) * 0.7f, 0),
            "SecondWind"     => Score(Exh(2) + Blk(10), 1),
            "Stoke"          => Score(Exh(5) + DrawPer * 5, 1), // 手牌5张
            "TrueGrit"       => Score(Blk(7) + Exh(), 1),
            "Unmovable"      => Score(Blk(7) * 2, 2),
            "Brand"          => Score(Exh() + StrPer + HpEffect(1), 0),
            "Juggling"       => Score(DrawPer * 2, 1),
            "Tremble"        => Score(VulnPer * 2, 1),

            // ── 能力牌（按单次效果算，不×回合数）────────────────────────
            "Aggression"     => Score(DrawPer, 1),
            "Barricade"      => Score(Blk(10), 3),
            "CrimsonMantle"  => Score(Blk(8) + HpEffect(1), 1),
            "Corruption"     => Score(EnergyPer * 5, 3),
            "Cruelty"        => Score(VulnPer * 2, 1),
            "DarkEmbrace"    => Score(DrawPer, 2),
            "DemonForm"      => Score(StrPer * 2, 3),
            "DrumOfBattle"   => Score(DrawPer + Exh(), 0),
            "Hellraiser"     => Score(Exh(), 2),
            "Inflame"        => Score(StrPer * 2, 1),
            "Inferno"        => Score(Dmg(4, true, aoeCountInDeck) + HpEffect(1), 1),
            "Juggernaut"     => Score(Dmg(5, false, 0) * 0.5f, 2),
            "OneTwoPunch"    => Score(DrawPer, 1),
            "Pyre"           => Score(EnergyPer, 2),
            "Rage"           => Score(Blk(3), 0),
            "Rupture"        => Score(StrPer, 1),
            "Stampede"       => Score(Dmg(7, false, 0) * 0.5f, 2),
            "StoneArmor"     => Score(Blk(4 * 5 / 2f), 1),  // 覆甲4层总格挡10
            "Tank"           => Score(Blk(3), 1),
            "Vicious"        => Score(DrawPer, 1),

            _ => 3.0f
        };
    }
}
