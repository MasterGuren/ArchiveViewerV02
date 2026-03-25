using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchiveViewer.Models;

namespace ArchiveViewer.Services;

public class ConfigService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".archive_viewer_v2_config.json");

    // Python版の設定ファイル（レガシー移行用、読み取り専用）
    private static readonly string LegacyConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".archive_viewer_config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static AppConfig Load()
    {
        try
        {
            // V2設定ファイルがあればそちらを使う
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? CreateDefault();
                MigrateFlatStateIfNeeded(json, config);
                return config;
            }

            // V2がなければPython版の設定を読み込む（読み取り専用、書き込みはしない）
            if (File.Exists(LegacyConfigPath))
            {
                var json = File.ReadAllText(LegacyConfigPath);
                var config = LoadFromLegacy(json);
                if (config != null) return config;
            }
        }
        catch { }
        return CreateDefault();
    }

    /// <summary>
    /// Python版JSONから読み込み。旧形式(promote/stay/demote)またはpresets構造の両方に対応。
    /// </summary>
    private static AppConfig? LoadFromLegacy(string json)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("promote", out _) ||
            root.TryGetProperty("stay", out _) ||
            root.TryGetProperty("demote", out _))
        {
            return MigrateLegacyConfig(root);
        }

        // Python版の新形式 → stateキーがないのでフラットなキーから読み込む
        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? CreateDefault();
        MigrateFlatStateIfNeeded(json, config);
        return config;
    }

    /// <summary>
    /// stateキーがなくフラットにcurrent_preset等が置かれている場合、Stateに移行する。
    /// </summary>
    private static void MigrateFlatStateIfNeeded(string json, AppConfig config)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // stateキーが既にあれば移行不要
        if (root.TryGetProperty("state", out _)) return;

        // フラットなキーからStateを構築
        if (root.TryGetProperty("extract_current_preset", out var ecp))
            config.State.ExtractCurrentPreset = ecp.GetString() ?? "";
        if (root.TryGetProperty("extract_output_folder", out var eof))
            config.State.ExtractOutputFolder = eof.GetString() ?? "";
        if (root.TryGetProperty("folder_sort", out var fs))
            config.State.FolderSort = fs.GetString() ?? "name";
        if (root.TryGetProperty("card_orient", out var co))
            config.State.CardOrient = co.GetString() ?? "portrait";
        if (root.TryGetProperty("video_volume", out var vv))
            config.State.VideoVolume = vv.GetInt32();
        if (root.TryGetProperty("video_end_action", out var vea))
            config.State.VideoEndAction = vea.GetString() ?? "stop";
        if (root.TryGetProperty("last_mode", out var lm))
            config.State.LastMode = lm.GetString() ?? "browse";

        // プリセット順序をDictionaryのキー順から取得
        config.State.ExtractPresetOrder = [.. config.ExtractPresets.Keys];
        config.State.ImagePresetOrder = [.. config.ImagePresets.Keys];
    }

    private static AppConfig MigrateLegacyConfig(JsonElement root)
    {
        var config = CreateDefault();
        var preset = new PresetData
        {
            Actions = GetDefaultActions()
        };

        var legacyMapping = new Dictionary<string, int>
        {
            { "demote", 0 }, { "trash", 1 }, { "stay", 2 },
            { "promote", 3 }, { "promote2", 4 }
        };

        foreach (var (key, actionIdx) in legacyMapping)
        {
            if (root.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.String)
            {
                var folder = val.GetString() ?? "";
                if (!string.IsNullOrEmpty(folder) && actionIdx < preset.Actions.Count)
                    preset.Actions[actionIdx].Folder = folder;
            }
        }

        if (root.TryGetProperty("source_folder", out var srcFolder) && srcFolder.ValueKind == JsonValueKind.String)
        {
            var f = srcFolder.GetString();
            if (!string.IsNullOrEmpty(f)) preset.SourceFolders.Add(f);
        }
        if (root.TryGetProperty("trash_folder", out var trashFolder) && trashFolder.ValueKind == JsonValueKind.String)
            preset.TrashFolder = trashFolder.GetString() ?? "";

        config.ExtractPresets["デフォルト"] = preset;
        config.ImagePresets["デフォルト"] = preset.Clone();
        return config;
    }

    /// <summary>
    /// 状態のみ保存（プリセット順序・選択中プリセット・UI設定）。プリセットデータには触らない。
    /// </summary>
    public static void SaveState(AppConfig config)
    {
        try
        {
            JsonNode? root;
            if (File.Exists(ConfigPath))
            {
                var existing = File.ReadAllText(ConfigPath);
                root = JsonNode.Parse(existing) ?? new JsonObject();
            }
            else
            {
                root = new JsonObject();
            }

            // stateセクションのみ上書き
            var stateJson = JsonSerializer.SerializeToNode(config.State, JsonOptions);
            root["state"] = stateJson;

            File.WriteAllText(ConfigPath, root.ToJsonString(JsonOptions));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigService] SaveState failed: {ex.Message}");
        }
    }

    /// <summary>
    /// プリセットデータを保存（「保存」ボタン押下時のみ呼ぶ）。状態も一緒に保存する。
    /// </summary>
    public static void SavePresets(AppConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigService] SavePresets failed: {ex.Message}");
        }
    }

    public static AppConfig CreateDefault()
    {
        var config = new AppConfig();
        var defaultPreset = new PresetData
        {
            Actions =
            [
                new() { Label = "撃墜", Color = "#ef4444" },
                new() { Label = "削除", Color = "#7f1d1d" },
                new() { Label = "ステイ", Color = "#3b82f6" },
                new() { Label = "昇格", Color = "#22c55e" },
                new() { Label = "2段階昇格", Color = "#f59e0b" }
            ]
        };
        config.ExtractPresets["デフォルト"] = defaultPreset;
        config.State.ExtractCurrentPreset = "デフォルト";
        config.ImagePresets["デフォルト"] = defaultPreset.Clone();
        config.State.ImageCurrentPreset = "デフォルト";
        config.RatingPresets["デフォルト"] = new RatingPresetData();
        config.State.RatingCurrentPreset = "デフォルト";
        config.VideoRatingPresets["デフォルト"] = new RatingPresetData();
        config.State.VideoRatingCurrentPreset = "デフォルト";
        return config;
    }

    public static List<ActionItem> GetDefaultActions() =>
    [
        new() { Label = "撃墜", Color = "#ef4444" },
        new() { Label = "削除", Color = "#7f1d1d" },
        new() { Label = "ステイ", Color = "#3b82f6" },
        new() { Label = "昇格", Color = "#22c55e" },
        new() { Label = "2段階昇格", Color = "#f59e0b" }
    ];
}
