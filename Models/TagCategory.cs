namespace ArchiveViewer.Models;

public class MajorCategory
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public string? Color { get; set; }
}

public class MinorCategory
{
    public long Id { get; set; }
    public long MajorCategoryId { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public string? Color { get; set; }
    public bool IsRequired { get; set; }
}
