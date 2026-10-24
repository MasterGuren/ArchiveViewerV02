namespace ArchiveViewer.Models;

/// <summary>名前を付けて登録したタグの組み合わせ。ファイル一覧の右クリックからまとめて貼り付けられる。</summary>
public class TagPreset
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public List<Tag> Tags { get; set; } = [];
}
