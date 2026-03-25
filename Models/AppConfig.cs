using System.Text.Json.Serialization;

namespace ArchiveViewer.Models;

public class AppConfig
{
    // === プリセットデータ（「保存」ボタンでのみ書き込み） ===
    [JsonPropertyName("extract_presets")]
    public Dictionary<string, PresetData> ExtractPresets { get; set; } = new();

    [JsonPropertyName("image_presets")]
    public Dictionary<string, PresetData> ImagePresets { get; set; } = new();

    [JsonPropertyName("rating_presets")]
    public Dictionary<string, RatingPresetData> RatingPresets { get; set; } = new();

    [JsonPropertyName("video_rating_presets")]
    public Dictionary<string, RatingPresetData> VideoRatingPresets { get; set; } = new();

    // === 状態（順序・選択・UI設定。自由に保存OK） ===
    [JsonPropertyName("state")]
    public AppState State { get; set; } = new();
}

public class AppState
{
    [JsonPropertyName("extract_current_preset")]
    public string ExtractCurrentPreset { get; set; } = "";

    [JsonPropertyName("extract_preset_order")]
    public List<string> ExtractPresetOrder { get; set; } = [];

    [JsonPropertyName("image_current_preset")]
    public string ImageCurrentPreset { get; set; } = "";

    [JsonPropertyName("image_preset_order")]
    public List<string> ImagePresetOrder { get; set; } = [];

    [JsonPropertyName("extract_output_folder")]
    public string ExtractOutputFolder { get; set; } = "";

    [JsonPropertyName("folder_sort")]
    public string FolderSort { get; set; } = "name";

    [JsonPropertyName("card_orient")]
    public string CardOrient { get; set; } = "portrait";

    [JsonPropertyName("video_volume")]
    public int VideoVolume { get; set; } = 80;

    [JsonPropertyName("video_end_action")]
    public string VideoEndAction { get; set; } = "stop";

    [JsonPropertyName("rating_current_preset")]
    public string RatingCurrentPreset { get; set; } = "";

    [JsonPropertyName("rating_preset_order")]
    public List<string> RatingPresetOrder { get; set; } = [];

    [JsonPropertyName("rating_judgment_level")]
    public int RatingJudgmentLevel { get; set; } = 0;

    [JsonPropertyName("video_rating_current_preset")]
    public string VideoRatingCurrentPreset { get; set; } = "";

    [JsonPropertyName("video_rating_preset_order")]
    public List<string> VideoRatingPresetOrder { get; set; } = [];

    [JsonPropertyName("video_rating_judgment_level")]
    public int VideoRatingJudgmentLevel { get; set; } = 0;

    [JsonPropertyName("last_mode")]
    public string LastMode { get; set; } = "browse";

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "";
}
