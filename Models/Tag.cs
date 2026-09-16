namespace ArchiveViewer.Models;

public class Tag
{
    public long Id { get; set; }
    public long? MinorCategoryId { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public string? Color { get; set; }
}
