namespace ArchiveViewer.Models;

public class ExtractEntry
{
    public int Start { get; set; }
    public int End { get; set; }
    public string Author { get; set; } = "";
    public string Title { get; set; } = "";
    public string Episode { get; set; } = "";
    public bool Extracted { get; set; }
}
