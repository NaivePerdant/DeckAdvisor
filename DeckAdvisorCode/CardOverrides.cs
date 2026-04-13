using System.Text.Json;

namespace DeckAdvisor.DeckAdvisorCode;

/// <summary>
/// 读取 card_overrides.json，提供两种自定义能力：
/// 1. scoreOverride：覆盖算法基础分（null = 使用算法）
/// 2. note：选牌建议文字，显示在卡牌下方信息框
///
/// 全局开关（_config 字段）：
///   showScore: 是否显示算法评分
///   showNote:  是否显示备注
/// </summary>
public static class CardOverrides
{
    record Entry(float? ScoreOverride, string? Note);

    static Dictionary<string, Entry> _data = new();

    /// <summary>是否显示评分（等级+分数），由 _config.showScore 控制。</summary>
    public static bool ShowScore { get; private set; } = false;

    /// <summary>是否显示备注，由 _config.showNote 控制。</summary>
    public static bool ShowNote  { get; private set; } = true;

    /// <summary>
    /// 从指定目录加载 card_overrides.json。
    /// 文件不存在时静默跳过，解析失败时写日志。
    /// </summary>
    public static void Load(string modDir)
    {
        var path = System.IO.Path.Combine(modDir, "card_overrides.json");
        if (!System.IO.File.Exists(path)) return;
        try
        {
            var json = System.IO.File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);

            // 读取全局配置
            if (doc.RootElement.TryGetProperty("_config", out var cfg))
            {
                if (cfg.TryGetProperty("showScore", out var ss)) ShowScore = ss.GetBoolean();
                if (cfg.TryGetProperty("showNote",  out var sn)) ShowNote  = sn.GetBoolean();
            }

            // 读取每张牌的覆盖数据（跳过 _ 开头的元数据字段）
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name.StartsWith("_")) continue;
                float? scoreOverride = null;
                string? note = null;
                if (prop.Value.TryGetProperty("scoreOverride", out var sv) && sv.ValueKind == JsonValueKind.Number)
                    scoreOverride = sv.GetSingle();
                if (prop.Value.TryGetProperty("note", out var nv) && nv.ValueKind == JsonValueKind.String)
                    note = nv.GetString();
                _data[prop.Name] = new Entry(scoreOverride, note);
            }
            MainFile.Logger.Info($"DeckAdvisor: Loaded {_data.Count} overrides. showScore={ShowScore} showNote={ShowNote}");
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"DeckAdvisor: Failed to load card_overrides.json: {ex.Message}");
        }
    }

    /// <summary>返回指定卡牌的分数覆盖值，无覆盖时返回 null。</summary>
    public static float? GetScoreOverride(string cardName) =>
        _data.TryGetValue(cardName, out var e) ? e.ScoreOverride : null;

    /// <summary>返回指定卡牌的备注文字，无备注时返回 null。</summary>
    public static string? GetNote(string cardName) =>
        _data.TryGetValue(cardName, out var e) ? e.Note : null;
}
