using System.Text.Json;
using Godot;

namespace DeckAdvisor.DeckAdvisorCode;

public static class CardOverrides
{
    record Entry(float? ScoreOverride, string? Note);

    static Dictionary<string, Entry> _data = new();
    public static bool ShowScore { get; private set; } = false;
    public static bool ShowNote  { get; private set; } = true;

    public static void Load(string modDir)
    {
        var path = System.IO.Path.Combine(modDir, "card_overrides.json");
        if (!System.IO.File.Exists(path)) return;
        try
        {
            var json = System.IO.File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);

            // 读取 _config
            if (doc.RootElement.TryGetProperty("_config", out var cfg))
            {
                if (cfg.TryGetProperty("showScore", out var ss)) ShowScore = ss.GetBoolean();
                if (cfg.TryGetProperty("showNote",  out var sn)) ShowNote  = sn.GetBoolean();
            }

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

    public static float? GetScoreOverride(string cardName) =>
        _data.TryGetValue(cardName, out var e) ? e.ScoreOverride : null;

    public static string? GetNote(string cardName) =>
        _data.TryGetValue(cardName, out var e) ? e.Note : null;
}
