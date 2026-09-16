namespace ArchiveViewer.Models;

/// <summary>
/// タグDB上でのファイルの識別単位。パス＋サイズ＋更新日時の複合キーで同一性を判定する
/// （リネーム・移動だけなら複合キーの一部が変わるため、新規エントリとして扱われる）。
/// </summary>
public class FileEntry
{
    public long Id { get; set; }
    public string FilePath { get; set; } = "";
    public long FileSize { get; set; }
    public long LastModifiedTicks { get; set; }
    public string? WorkTitle { get; set; }
}
