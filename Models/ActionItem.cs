using System.Text.Json.Serialization;

namespace ArchiveViewer.Models;

public class ActionItem
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#ef4444";

    [JsonPropertyName("folder")]
    public string Folder { get; set; } = "";

    [JsonPropertyName("copy")]
    public bool Copy { get; set; }

    public ActionItem Clone() => new()
    {
        Label = Label,
        Color = Color,
        Folder = Folder,
        Copy = Copy
    };
}
