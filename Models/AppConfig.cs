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

    [JsonPropertyName("folder_sort_dir")]
    public string FolderSortDir { get; set; } = "asc";

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

    [JsonPropertyName("video_scroll_amount")]
    public string VideoScrollAmount { get; set; } = "30";

    [JsonPropertyName("thumb_size")]
    public int ThumbSize { get; set; } = 480;

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "";

    [JsonPropertyName("image_scale_mode")]
    public string ImageScaleMode { get; set; } = "Default";

    [JsonPropertyName("tag_db_mode")]
    public string TagDbMode { get; set; } = "demo";

    [JsonPropertyName("tag_source_folders")]
    public List<string> TagSourceFolders { get; set; } = [];

    [JsonPropertyName("tag_disabled_folders")]
    public List<string> TagDisabledFolders { get; set; } = [];

    [JsonPropertyName("tag_picker_width")]
    public double TagPickerDialogWidth { get; set; } = 1240;

    [JsonPropertyName("tag_picker_height")]
    public double TagPickerDialogHeight { get; set; } = 720;

    [JsonPropertyName("tag_search_tag_ids")]
    public List<long> TagSearchTagIds { get; set; } = [];

    [JsonPropertyName("tag_search_match_all")]
    public bool TagSearchMatchAll { get; set; }

    [JsonPropertyName("tag_search_text")]
    public string TagSearchText { get; set; } = "";

    [JsonPropertyName("tag_search_no_tags")]
    public bool TagSearchNoTags { get; set; }

    [JsonPropertyName("tag_search_has_tags")]
    public bool TagSearchHasTags { get; set; }

    [JsonPropertyName("tag_search_no_title")]
    public bool TagSearchNoTitle { get; set; }

    [JsonPropertyName("tag_search_has_title")]
    public bool TagSearchHasTitle { get; set; }

    [JsonPropertyName("tag_video_source_folders")]
    public List<string> TagVideoSourceFolders { get; set; } = [];

    [JsonPropertyName("tag_video_disabled_folders")]
    public List<string> TagVideoDisabledFolders { get; set; } = [];

    [JsonPropertyName("tag_video_search_tag_ids")]
    public List<long> TagVideoSearchTagIds { get; set; } = [];

    [JsonPropertyName("tag_video_search_match_all")]
    public bool TagVideoSearchMatchAll { get; set; }

    [JsonPropertyName("tag_video_search_text")]
    public string TagVideoSearchText { get; set; } = "";

    [JsonPropertyName("tag_video_search_no_tags")]
    public bool TagVideoSearchNoTags { get; set; }

    [JsonPropertyName("tag_video_search_has_tags")]
    public bool TagVideoSearchHasTags { get; set; }

    [JsonPropertyName("tag_video_search_no_title")]
    public bool TagVideoSearchNoTitle { get; set; }

    [JsonPropertyName("tag_video_search_has_title")]
    public bool TagVideoSearchHasTitle { get; set; }

    [JsonPropertyName("tag_checklist_sort_by_usage")]
    public bool TagChecklistSortByUsage { get; set; }

    [JsonPropertyName("tag_move_folders")]
    public List<string> TagMoveFolders { get; set; } = [];

    [JsonPropertyName("tag_move_target_folder")]
    public string? TagMoveTargetFolder { get; set; }

    [JsonPropertyName("tag_video_move_folders")]
    public List<string> TagVideoMoveFolders { get; set; } = [];

    [JsonPropertyName("tag_video_move_target_folder")]
    public string? TagVideoMoveTargetFolder { get; set; }
}
