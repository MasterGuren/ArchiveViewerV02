using System.Text.Json.Serialization;

namespace ArchiveViewer.Models;

public class RatingPresetData
{
    /// <summary>10 rating folders: index 0 = ★0, index 9 = ★9</summary>
    [JsonPropertyName("rating_folders")]
    public List<string> RatingFolders { get; set; } = new(Enumerable.Repeat("", 10));

    /// <summary>10 confirmed folders: index 0 = ★0確, index 9 = ★9確</summary>
    [JsonPropertyName("confirmed_folders")]
    public List<string> ConfirmedFolders { get; set; } = new(Enumerable.Repeat("", 10));

    /// <summary>10 delete folders: index 0 = ★0削除, index 9 = ★9削除</summary>
    [JsonPropertyName("delete_folders")]
    public List<string> DeleteFolders { get; set; } = new(Enumerable.Repeat("", 10));

    /// <summary>Source folders per judgment level (key = level 0-8, value = list of source folders)</summary>
    [JsonPropertyName("source_folders")]
    public Dictionary<int, List<string>> SourceFolders { get; set; } = new();

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    public RatingPresetData Clone()
    {
        return new RatingPresetData
        {
            RatingFolders = new List<string>(RatingFolders),
            ConfirmedFolders = new List<string>(ConfirmedFolders),
            DeleteFolders = new List<string>(DeleteFolders),
            SourceFolders = SourceFolders.ToDictionary(
                kv => kv.Key,
                kv => new List<string>(kv.Value)),
            Category = Category
        };
    }
}
