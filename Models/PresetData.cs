using System.Text.Json.Serialization;

namespace ArchiveViewer.Models;

public class PresetData
{
    [JsonPropertyName("actions")]
    public List<ActionItem> Actions { get; set; } = [];

    [JsonPropertyName("source_folders")]
    public List<string> SourceFolders { get; set; } = [];

    [JsonPropertyName("video_folders")]
    public List<string> VideoFolders { get; set; } = [];

    [JsonPropertyName("trash_folder")]
    public string TrashFolder { get; set; } = "";

    public PresetData Clone()
    {
        return new PresetData
        {
            Actions = Actions.Select(a => a.Clone()).ToList(),
            SourceFolders = new List<string>(SourceFolders),
            VideoFolders = new List<string>(VideoFolders),
            TrashFolder = TrashFolder
        };
    }
}
