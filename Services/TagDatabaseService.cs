using System.IO;
using Microsoft.Data.Sqlite;

namespace ArchiveViewer.Services;

public enum TagDbMode
{
    Demo,
    Production
}

/// <summary>
/// タグ管理用SQLiteデータベースの接続とスキーマ初期化を担う。
/// デモDBと本番DBは別ファイルとして完全に分離される。
/// </summary>
public static class TagDatabaseService
{
    private static readonly string ProductionDbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".archive_viewer_v2_tags.db");

    private static readonly string DemoDbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".archive_viewer_v2_tags_demo.db");

    public static TagDbMode CurrentMode { get; private set; } = TagDbMode.Demo;

    private static readonly HashSet<string> _initializedPaths = [];

    public static void SetMode(TagDbMode mode)
    {
        CurrentMode = mode;
    }

    private static string CurrentDbPath => CurrentMode == TagDbMode.Demo ? DemoDbPath : ProductionDbPath;

    public static SqliteConnection CreateConnection()
    {
        var path = CurrentDbPath;
        var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        if (!_initializedPaths.Contains(path))
        {
            EnsureSchema(connection);
            _initializedPaths.Add(path);
            if (CurrentMode == TagDbMode.Demo)
                SeedDemoDataIfEmpty(connection);
        }

        return connection;
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS MajorCategories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                Color TEXT
            );

            CREATE TABLE IF NOT EXISTS MinorCategories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MajorCategoryId INTEGER NOT NULL REFERENCES MajorCategories(Id) ON DELETE CASCADE,
                Name TEXT NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                Color TEXT,
                IsRequired INTEGER NOT NULL DEFAULT 0,
                UNIQUE(MajorCategoryId, Name)
            );

            CREATE TABLE IF NOT EXISTS Tags (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MinorCategoryId INTEGER REFERENCES MinorCategories(Id) ON DELETE SET NULL,
                Name TEXT NOT NULL UNIQUE,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                Color TEXT
            );

            CREATE TABLE IF NOT EXISTS FileEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FilePath TEXT NOT NULL,
                FileSize INTEGER NOT NULL,
                LastModifiedTicks INTEGER NOT NULL,
                WorkTitle TEXT,
                UNIQUE(FilePath, FileSize, LastModifiedTicks)
            );

            CREATE TABLE IF NOT EXISTS FileTags (
                FileEntryId INTEGER NOT NULL REFERENCES FileEntries(Id) ON DELETE CASCADE,
                TagId INTEGER NOT NULL REFERENCES Tags(Id) ON DELETE CASCADE,
                PRIMARY KEY (FileEntryId, TagId)
            );

            CREATE INDEX IF NOT EXISTS idx_fileentries_path ON FileEntries(FilePath);
            CREATE INDEX IF NOT EXISTS idx_filetags_tag ON FileTags(TagId);
            """;
        cmd.ExecuteNonQuery();

        // 既存DB（Color列追加前に作られたファイル）向けのマイグレーション
        EnsureColumn(connection, "MajorCategories", "Color", "TEXT");
        EnsureColumn(connection, "MinorCategories", "Color", "TEXT");
        EnsureColumn(connection, "Tags", "Color", "TEXT");
        EnsureColumn(connection, "MinorCategories", "IsRequired", "INTEGER NOT NULL DEFAULT 0");
    }

    /// <summary>
    /// テーブルに指定カラムが無ければALTER TABLEで追加する（スキーマ情報のみ参照、行データは読まない）。
    /// </summary>
    private static void EnsureColumn(SqliteConnection connection, string table, string column, string typeDef)
    {
        using (var check = connection.CreateCommand())
        {
            check.CommandText = $"PRAGMA table_info({table});";
            using var reader = check.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {typeDef};";
        alter.ExecuteNonQuery();
    }

    /// <summary>
    /// デモDBが空の場合のみ、動作確認用のサンプルデータを投入する。本番DBには絶対に呼ばれない。
    /// </summary>
    private static void SeedDemoDataIfEmpty(SqliteConnection connection)
    {
        using (var check = connection.CreateCommand())
        {
            check.CommandText = "SELECT COUNT(*) FROM MajorCategories;";
            if ((long)check.ExecuteScalar()! > 0) return;
        }

        long InsertMajor(string name, int sort, string color)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO MajorCategories (Name, SortOrder, Color) VALUES ($n, $s, $c); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$n", name);
            cmd.Parameters.AddWithValue("$s", sort);
            cmd.Parameters.AddWithValue("$c", color);
            return (long)cmd.ExecuteScalar()!;
        }

        long InsertMinor(long majorId, string name, int sort, string color)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO MinorCategories (MajorCategoryId, Name, SortOrder, Color) VALUES ($m, $n, $s, $c); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$m", majorId);
            cmd.Parameters.AddWithValue("$n", name);
            cmd.Parameters.AddWithValue("$s", sort);
            cmd.Parameters.AddWithValue("$c", color);
            return (long)cmd.ExecuteScalar()!;
        }

        void InsertTag(long minorId, string name, int sort, string color)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO Tags (MinorCategoryId, Name, SortOrder, Color) VALUES ($m, $n, $s, $c);";
            cmd.Parameters.AddWithValue("$m", minorId);
            cmd.Parameters.AddWithValue("$n", name);
            cmd.Parameters.AddWithValue("$s", sort);
            cmd.Parameters.AddWithValue("$c", color);
            cmd.ExecuteNonQuery();
        }

        var genre = InsertMajor("ジャンル", 0, "#F0E68C");
        var fantasy = InsertMinor(genre, "ファンタジー", 0, "#EE82EE");
        InsertTag(fantasy, "異世界", 0, "#E6E6FA");
        InsertTag(fantasy, "魔法", 1, "#9966CC");
        var scifi = InsertMinor(genre, "SF", 1, "#4682B4");
        InsertTag(scifi, "ロボット", 0, "#808080");
        InsertTag(scifi, "宇宙", 1, "#000080");
        var daily = InsertMinor(genre, "日常", 2, "#98FF98");
        InsertTag(daily, "学園", 0, "#87CEEB");
        InsertTag(daily, "恋愛", 1, "#FFC0CB");

        var attr = InsertMajor("属性", 1, "#db2777");
        var chara = InsertMinor(attr, "キャラ", 0, "#FF7F50");
        InsertTag(chara, "かわいい", 0, "#FF69B4");
        InsertTag(chara, "かっこいい", 1, "#191970");
        var mood = InsertMinor(attr, "雰囲気", 1, "#800000");
        InsertTag(mood, "シリアス", 0, "#000000");
        InsertTag(mood, "コメディ", 1, "#FBC02D");

        var format = InsertMajor("作品形式", 2, "#FF0000");
        var oneshot = InsertMinor(format, "単話", 0, "#FBC02D");
        InsertTag(oneshot, "短編", 0, "#FFFACD");
        InsertTag(oneshot, "読み切り", 1, "#F0E68C");
        InsertTag(oneshot, "連載", 2, "#FF6347");

        var style = InsertMajor("画風", 3, "#40E0D0");
        var paint = InsertMinor(style, "塗り方", 0, "#3CB371");
        InsertTag(paint, "厚塗り", 0, "#D2691E");
        InsertTag(paint, "薄塗り", 1, "#FFFACD");
        InsertTag(paint, "セル塗り", 2, "#00FFFF");
        var line = InsertMinor(style, "線", 1, "#C0C0C0");
        InsertTag(line, "太線", 0, "#A9A9A9");
        InsertTag(line, "繊細線", 1, "#D3D3D3");

        var status = InsertMajor("状態", 4, "#008000");
        var evaluation = InsertMinor(status, "評価", 0, "#FFD700");
        InsertTag(evaluation, "お気に入り", 0, "#DC143C");
        InsertTag(evaluation, "保留", 1, "#BDB76B");
        InsertTag(evaluation, "要確認", 2, "#FFFF00");
        var progress = InsertMinor(status, "進捗", 1, "#B0E0E6");
        InsertTag(progress, "完了", 0, "#008000");
        InsertTag(progress, "未整理", 1, "#A9A9A9");
    }
}
