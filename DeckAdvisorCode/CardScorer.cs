using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace DeckAdvisor.DeckAdvisorCode;

public static class CardScorer
{
    public static readonly Dictionary<ModelId, (float score, string grade)> Current = new();

    // ── 基础分表（依据攻略评级） ──────────────────────────────────────────
    static readonly Dictionary<string, float> BaseScores = new()
    {
        // SSS
        { "Offering",       9.5f },
        { "Stoke",          9.5f },
        // SS
        { "BurningPact",    8.5f },
        { "Bloodletting",   8.5f },
        { "TrueGrit",       8.5f },
        { "PommelStrike",   8.5f },
        { "ShrugItOff",     8.5f },
        { "BattleTrance",   8.5f },
        { "DarkEmbrace",    8.0f },
        { "Armaments",      8.0f },
        { "ExpectAFight",   8.0f },
        // S
        { "Inflame",        7.5f },
        { "Thrash",         7.5f },
        { "Rupture",        7.5f },
        { "Conflagration",  7.5f },
        { "TearAsunder",    7.5f },
        { "CrimsonMantle",  7.5f },
        { "Headbutt",       7.5f },
        { "Pillage",        7.5f },
        { "ForgottenRitual",7.5f },
        { "Aggression",     7.0f },
        { "Brand",          6.0f },
        // A
        { "Rage",           6.5f },
        { "Taunt",          6.5f },
        { "Dominate",       6.5f },
        { "Barricade",      6.5f },
        { "BodySlam",       6.5f },
        { "Impervious",     6.5f },
        { "Whirlwind",      6.5f },
        { "Dismantle",      6.5f },
        { "Bully",          6.5f },
        { "Colossus",       6.5f },
        { "SecondWind",     6.5f },
        { "Unmovable",      6.5f },
        { "Feed",           6.5f },
        { "Hemokinesis",    6.0f },
        { "Breakthrough",   6.0f },
        { "Stampede",       6.0f },
        { "Pyre",           6.0f },
        { "FightMe",        6.0f },
        { "Mangle",         6.0f },
        { "Unrelenting",    6.0f },
        { "PrimalForce",    6.0f },
        { "FiendFire",      6.0f },
        { "EvilEye",        6.0f },
        // B
        { "Spite",          5.0f },
        { "TwinStrike",     5.0f },
        { "Uppercut",       5.0f },
        { "FlameBarrier",   5.0f },
        { "BloodWall",      4.5f },
        { "DrumOfBattle",   4.5f },
        { "MoltenFist",     4.5f },
        { "SwordBoomerang", 4.5f },
        { "Rampage",        4.5f },
        { "PactsEnd",       4.5f },
        { "Cruelty",        4.5f },
        // C
        { "StoneArmor",     3.0f },
        { "Juggernaut",     3.0f },
        { "Inferno",        3.0f },
        { "Hellraiser",     3.0f },
        // D
        { "InfernalBlade",  1.5f },
        { "Cascade",        1.5f },
        { "Bludgeon",       1.5f },
        { "Havoc",          1.5f },
    };

    // ── 流派检测阈值 ─────────────────────────────────────────────────────
    // 主动失血源（造成 Unblockable 自伤的牌，含绯红披风每回合自动触发）
    // 注意：好勇斗狠(Aggression)是从弃牌堆取攻击牌，不是失血
    static readonly HashSet<string> SelfDamageCards = new()
        { "Bloodletting", "BloodWall", "Breakthrough", "DemonicShield", "Hemokinesis", "Offering", "CrimsonMantle" };

    // 易伤源（给敌人施加 Vulnerable 的牌）
    static readonly HashSet<string> VulnerableCards = new()
        { "Bash", "Break", "Bully", "Colossus", "Dismantle", "Dominate", "MoltenFist",
          "Taunt", "Thunderclap", "Tremble", "Uppercut", "Vicious" };

    // 消耗相关牌（消耗手牌触发效果）
    static readonly HashSet<string> ExhaustCards = new()
        { "DarkEmbrace", "FeelNoPain", "SecondWind", "Corruption", "PactsEnd", "Unmovable", "Stoke", "BurningPact" };

    // 格挡流核心牌
    static readonly HashSet<string> BlockCards = new()
        { "Barricade", "BodySlam", "Impervious", "TrueGrit", "EvilEye", "CrimsonMantle" };

    // ── 重复惩罚：已有N张时再拿同名牌降分 ──────────────────────────────
    // 只需1张：能力牌 + 攻略明确说拿一张就够的
    static readonly HashSet<string> MaxOne = new()
    {
        // 能力牌（挂上生效，多张无意义）
        "Rupture", "CrimsonMantle", "Inflame", "Rage", "Barricade",
        "DarkEmbrace", "FeelNoPain", "DemonForm", "Pyre", "Aggression",
        "Corruption", "Juggernaut", "Hellraiser", "Vicious", "Stampede",
        // 攻略明确说拿一张
        "Offering", "Headbutt", "PerfectedStrike", "BattleTrance",
    };

    // 最多2张：攻略说1-2张的
    static readonly HashSet<string> MaxTwo = new()
        { "Bloodletting", "BurningPact", "PommelStrike", "ShrugItOff" };

    // 前期强/后期弱的牌：楼层越高降分
    // 每超过阈值楼层降 penaltyPerFloor 分
    static readonly Dictionary<string, (int thresholdFloor, float penaltyPerFloor)> EarlyGameCards = new()
    {
        { "Mangle",       (10, 0.3f) },  // 攻略：抓的早不错，后面抓到不考虑
        { "Breakthrough", (12, 0.2f) },  // 攻略：一层可以无脑抓，一层以后抓取位有所下降
        { "Unrelenting",  (12, 0.2f) },  // 攻略：一层推荐抓取，二层缺输出也可以
        { "PrimalForce",  (15, 0.2f) },  // 攻略：过渡舒服，后期价值下降
        { "Thrash",       (10, 0.2f) },  // 攻略：一层最强过渡，二层起贬值
    };

    static readonly Dictionary<string, (HashSet<string> targets, float bonus)[]> Synergies = new()
    {
        // 易伤体系：有燃烧(Inflame/Vicious)时，易伤源价值大幅提升
        ["Inflame"]      = [( new(){"Taunt","Thunderclap","Uppercut","Colossus","Bully","FightMe","Dismantle","Bash","Break","Vicious"}, 1.5f )],
        ["Vicious"]      = [( new(){"Taunt","Thunderclap","Uppercut","Colossus","Bully","Dismantle","Bash","Break","Inflame"}, 1.0f )],
        // 自残体系：有撕裂(Rupture)时，主动失血源价值提升
        ["Rupture"]      = [( new(){"Offering","Bloodletting","CrimsonMantle","BloodWall","Hemokinesis","Breakthrough","DemonicShield","TearAsunder"}, 1.5f )],
        // 自残体系：有扯碎(TearAsunder)时，失血源价值提升
        ["TearAsunder"]  = [( new(){"Offering","Bloodletting","CrimsonMantle","Rupture","BloodWall","Hemokinesis"}, 1.5f )],
        // 自残体系：有失血源时，扯碎和撕裂价值提升
        ["Bloodletting"] = [( new(){"TearAsunder","Rupture","Conflagration"}, 1.0f )],
        ["BloodWall"]    = [( new(){"TearAsunder","Rupture"}, 1.0f )],
        ["Hemokinesis"]  = [( new(){"TearAsunder","Rupture"}, 1.0f )],
        // 焚烧(Conflagration)：本回合打出攻击牌越多伤害越高，多攻击牌时价值提升
        ["Conflagration"]= [( new(){"PommelStrike","Whirlwind","TwinStrike","SwordBoomerang","Thrash","Dismantle"}, 1.0f )],
        // 格挡体系：壁垒和全身撞击互相依赖
        ["Barricade"]    = [( new(){"BodySlam","TrueGrit","Impervious","EvilEye","CrimsonMantle"}, 2.0f )],
        ["BodySlam"]     = [( new(){"Barricade","TrueGrit","Impervious"}, 2.0f )],
        // 消耗体系
        ["DarkEmbrace"]  = [( new(){"SecondWind","Corruption","Unmovable","FeelNoPain","Stoke","BurningPact"}, 1.5f )],
        // 易伤翻倍combo
        ["Dominate"]     = [( new(){"MoltenFist","Inflame","Taunt","Thunderclap","Vicious"}, 1.5f )],
        ["MoltenFist"]   = [( new(){"Dominate","Inflame","Vicious"}, 1.5f )],
        // 凌虐(Mangle)：吃力量加成，有力量来源时价值提升
        ["Mangle"]       = [( new(){"Rupture","Inflame","DemonForm","Inferno","Vicious"}, 1.0f )],
        // 剑柄打击+放血可无限
        ["PommelStrike"] = [( new(){"Bloodletting","ExpectAFight","ForgottenRitual"}, 1.0f )],
        // 燃烧(Inflame)本身：有易伤源时价值提升
        ["Taunt"]        = [( new(){"Inflame","Vicious"}, 1.0f )],
        ["Thunderclap"]  = [( new(){"Inflame","Vicious"}, 1.0f )],
    };

    // ── 公开接口 ─────────────────────────────────────────────────────────
    public static void Evaluate(Player player, IReadOnlyList<CardModel> options)
    {
        Current.Clear();
        var deck = player.Deck.Cards.ToList();
        var deckNames = deck.Select(c => c.GetType().Name).ToHashSet();
        int floor = player.RunState?.TotalFloor ?? 0;

        // 检测流派倾向
        int selfDmgCount  = deckNames.Count(n => SelfDamageCards.Contains(n));
        bool hasCrimson   = deckNames.Contains("CrimsonMantle");
        bool hasRupture   = deckNames.Contains("Rupture");
        int vulnCount     = deckNames.Count(n => VulnerableCards.Contains(n));
        int exhaustCount  = deckNames.Count(n => ExhaustCards.Contains(n));
        int blockCount    = deckNames.Count(n => BlockCards.Contains(n));

        // 自残流：有≥2张失血源，或有撕裂/绯红披风+≥1张失血源
        bool isBleedBuild   = selfDmgCount >= 2 || ((hasRupture || hasCrimson) && selfDmgCount >= 1);
        bool isVulnBuild    = vulnCount >= 2;
        bool isExhaustBuild = exhaustCount >= 2;
        bool isBlockBuild   = blockCount >= 2;

        foreach (var card in options)
        {
            float s = ScoreCard(card, deck, deckNames, isBleedBuild, isVulnBuild, isExhaustBuild, isBlockBuild, floor);
            string grade = s >= 9f ? "S" : s >= 7f ? "A" : s >= 5f ? "B" : s >= 3f ? "C" : "D";
            Current[card.Id] = (s, grade);
        }
    }

    public static void EvaluateShopCard(MegaCrit.Sts2.Core.Nodes.Cards.NCard triggerCard)
    {
        try
        {
            var state = RunManager.Instance.DebugOnlyGetState();
            if (state == null) return;
            var player = state.Players.FirstOrDefault();
            if (player == null) return;
            var card = triggerCard.Model;
            if (card == null) return;

            var deck = player.Deck.Cards.ToList();
            var deckNames = deck.Select(c => c.GetType().Name).ToHashSet();
            int floor = player.RunState?.TotalFloor ?? 0;
            int selfDmgCount  = deckNames.Count(n => SelfDamageCards.Contains(n));
            bool hasCrimson   = deckNames.Contains("CrimsonMantle");
            bool hasRupture   = deckNames.Contains("Rupture");
            int vulnCount     = deckNames.Count(n => VulnerableCards.Contains(n));
            int exhaustCount  = deckNames.Count(n => ExhaustCards.Contains(n));
            int blockCount    = deckNames.Count(n => BlockCards.Contains(n));
            bool isBleedBuild   = selfDmgCount >= 2 || ((hasRupture || hasCrimson) && selfDmgCount >= 1);
            bool isVulnBuild    = vulnCount >= 2;
            bool isExhaustBuild = exhaustCount >= 2;
            bool isBlockBuild   = blockCount >= 2;

            float s = ScoreCard(card, deck, deckNames, isBleedBuild, isVulnBuild, isExhaustBuild, isBlockBuild, floor);
            string grade = s >= 9f ? "S" : s >= 7f ? "A" : s >= 5f ? "B" : s >= 3f ? "C" : "D";
            Current[card.Id] = (s, grade);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"DeckAdvisor: EvaluateShopCard failed: {ex.Message}");
        }
    }

    public static void EvaluateFromReflection(MegaCrit.Sts2.Core.Nodes.Cards.NCard triggerCard)
    {
        try
        {
            var state = RunManager.Instance.DebugOnlyGetState();
            if (state == null) { MainFile.Logger.Info("DeckAdvisor: RunState is null"); return; }

            var player = state.Players.FirstOrDefault();
            if (player == null) { MainFile.Logger.Info("DeckAdvisor: No players in state"); return; }

            var cards = FindRewardCards(triggerCard);
            if (cards == null || cards.Count == 0) { MainFile.Logger.Info("DeckAdvisor: No reward cards found"); return; }

            MainFile.Logger.Info($"DeckAdvisor: Evaluating {cards.Count} cards.");
            Evaluate(player, cards);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"DeckAdvisor: EvaluateFromReflection failed: {ex.Message}");
        }
    }

    // ── 核心评分逻辑 ─────────────────────────────────────────────────────
    static float ScoreCard(CardModel card, List<CardModel> deck, HashSet<string> deckNames,
        bool isBleedBuild, bool isVulnBuild, bool isExhaustBuild, bool isBlockBuild, int floor)
    {
        string name = card.GetType().Name;

        // 1. 基础分
        float s = BaseScores.TryGetValue(name, out var baseScore) ? baseScore : DefaultScore(card);

        // 2. 联动加分：deck 中已有的牌对当前候选牌加分
        foreach (var (key, rules) in Synergies)
        {
            if (!deckNames.Contains(key)) continue;
            foreach (var (targets, bonus) in rules)
                if (targets.Contains(name)) s += bonus;
        }

        // 3. 流派契合加分
        if (isBleedBuild   && (SelfDamageCards.Contains(name) || name == "Rupture" || name == "TearAsunder" || name == "CrimsonMantle")) s += 1.0f;
        if (isVulnBuild    && VulnerableCards.Contains(name))   s += 1.0f;
        if (isExhaustBuild && ExhaustCards.Contains(name))      s += 1.0f;
        if (isBlockBuild   && BlockCards.Contains(name))        s += 1.0f;

        // 4. 特殊惩罚
        // 重复惩罚
        int copies = deck.Count(c => c.GetType().Name == name);
        if (copies >= 1 && MaxOne.Contains(name))  s -= 4.0f;
        if (copies >= 2 && MaxTwo.Contains(name))  s -= 3.0f;
        // 能力牌通用：超过1张大幅降分
        if (copies >= 1 && card.Type == CardType.Power) s -= 3.0f;
        // 壁垒/全身撞击单独出现时价值大幅下降（需要配合）
        if (name == "Barricade" && !deckNames.Contains("BodySlam"))   s -= 1.5f;
        if (name == "BodySlam"  && !deckNames.Contains("Barricade"))  s -= 1.5f;
        // 主宰没有易伤源时价值下降
        if (name == "Dominate" && vulnCount(deckNames) == 0)          s -= 2.0f;
        // 残酷没有易伤源时价值下降
        if (name == "Cruelty"  && vulnCount(deckNames) == 0)          s -= 2.0f;
        // 扯碎没有失血源时价值下降
        if (name == "TearAsunder" && bleedCount(deckNames) == 0)      s -= 2.0f;
        // 战鼓随机烧牌有风险，卡组有关键牌时降分
        if (name == "DrumOfBattle" && deck.Count > 12)                s -= 1.0f;

        // 楼层惩罚：前期强牌在后期降分
        if (EarlyGameCards.TryGetValue(name, out var earlyGame) && floor > earlyGame.thresholdFloor)
            s -= (floor - earlyGame.thresholdFloor) * earlyGame.penaltyPerFloor;

        return Math.Clamp(s, 0f, 10f);
    }

    static int vulnCount(HashSet<string> deckNames)  => deckNames.Count(n => VulnerableCards.Contains(n));
    static int bleedCount(HashSet<string> deckNames) => deckNames.Count(n => SelfDamageCards.Contains(n));

    // 未在表中的卡用稀有度给默认分
    static float DefaultScore(CardModel card) => card.Rarity switch
    {
        CardRarity.Rare     => 5.0f,
        CardRarity.Uncommon => 4.0f,
        _                   => 3.0f,
    };

    static List<CardModel>? FindRewardCards(MegaCrit.Sts2.Core.Nodes.Cards.NCard triggerCard)
    {
        var node = triggerCard.GetParent();
        while (node != null)
        {
            if (node is MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen screen)
            {
                var field = screen.GetType().GetField("_options",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(screen) is IReadOnlyList<MegaCrit.Sts2.Core.Entities.Cards.CardCreationResult> options)
                    return options.Select(o => o.Card).ToList();
                return null;
            }
            node = node.GetParent();
        }
        return null;
    }
}
