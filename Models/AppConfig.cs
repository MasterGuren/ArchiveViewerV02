using System.Text.Json.Serialization;

namespace ArchiveViewer.Models;

public class AppConfig
{
    // === プリセットデータ（「保存」ボタンでのみ書き込み） ===
    [JsonPropertyName("presets")]
    public Dictionary<string, PresetData> Presets { get; set; } = new();

    [JsonPropertyName("extract_presets")]
    public Dictionary<string, PresetData> ExtractPresets { get; set; } = new();

    [JsonPropertyName("video_presets")]
    public Dictionary<string, PresetData> VideoPresets { get; set; } = new();

    [JsonPropertyName("image_presets")]
    public Dictionary<string, PresetData> ImagePresets { get; set; } = new();

    // === 状態（順序・選択・UI設定。自由に保存OK） ===
    [JsonPropertyName("state")]
    public AppState State { get; set; } = new();
}

public class AppState
{
    [JsonPropertyName("current_preset")]
    public string CurrentPreset { get; set; } = "";

    [JsonPropertyName("extract_current_preset")]
    public string ExtractCurrentPreset { get; set; } = "";

    [JsonPropertyName("video_current_preset")]
    public string VideoCurrentPreset { get; set; } = "";

    [JsonPropertyName("preset_order")]
    public List<string> PresetOrder { get; set; } = [];

    [JsonPropertyName("extract_preset_order")]
    public List<string> ExtractPresetOrder { get; set; } = [];

    [JsonPropertyName("video_preset_order")]
    public List<string> VideoPresetOrder { get; set; } = [];

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

    [JsonPropertyName("last_mode")]
    public string LastMode { get; set; } = "browse";

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "";
}
