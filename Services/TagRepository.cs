using ArchiveViewer.Models;
using Microsoft.Data.Sqlite;

namespace ArchiveViewer.Services;

/// <summary>画像用タグ体系と動画用タグ体系のどちらを操作するかを選ぶ。テーブルは完全に分離されている。</summary>
public enum TagDomain
{
    Image,
    Video
}

/// <summary>
/// タグ・カテゴリ・ファイル紐付けのCRUDを提供する。
/// domain引数（既定はImage）でテーブル一式を画像用/動画用に切り替える。
/// </summary>
public static class TagRepository
{
    private static (string Major, string Minor, string Tags, string FileEntries, string FileTags) Tables(TagDomain domain) => domain switch
    {
        TagDomain.Video => ("VideoMajorCategories", "VideoMinorCategories", "VideoTags", "VideoFileEntries", "VideoFileTags"),
        _ => ("MajorCategories", "MinorCategories", "Tags", "FileEntries", "FileTags")
    };

    // === 大カテゴリ ===

    public static List<MajorCategory> GetMajorCategories(TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Id, Name, SortOrder, Color FROM {tbl.Major} ORDER BY SortOrder, Name;";
        using var reader = cmd.ExecuteReader();
        var result = new List<MajorCategory>();
        while (reader.Read())
        {
            result.Add(new MajorCategory
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2),
                Color = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }
        return result;
    }

    public static long AddMajorCategory(string name, int sortOrder = 0, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO {tbl.Major} (Name, SortOrder) VALUES ($name, $sort); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$sort", sortOrder);
        return (long)cmd.ExecuteScalar()!;
    }

    public static void SetMajorCategoryColor(long id, string? color, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {tbl.Major} SET Color = $color WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$color", (object?)color ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public static void RenameMajorCategory(long id, string newName, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {tbl.Major} SET Name = $name WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$name", newName);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 渡された順序で大カテゴリのSortOrderを振り直す（ドラッグ&ドロップによる並べ替え用）。
    /// </summary>
    public static void SetMajorCategorySortOrders(List<long> orderedIds, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var tx = conn.BeginTransaction();
        for (int i = 0; i < orderedIds.Count; i++)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $"UPDATE {tbl.Major} SET SortOrder = $sort WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$sort", i);
            cmd.Parameters.AddWithValue("$id", orderedIds[i]);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public static void DeleteMajorCategory(long id, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM {tbl.Major} WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // === 中カテゴリ ===

    public static List<MinorCategory> GetMinorCategories(long majorCategoryId, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Id, MajorCategoryId, Name, SortOrder, Color, IsRequired FROM {tbl.Minor} WHERE MajorCategoryId = $majorId ORDER BY SortOrder, Name;";
        cmd.Parameters.AddWithValue("$majorId", majorCategoryId);
        using var reader = cmd.ExecuteReader();
        var result = new List<MinorCategory>();
        while (reader.Read())
        {
            result.Add(new MinorCategory
            {
                Id = reader.GetInt64(0),
                MajorCategoryId = reader.GetInt64(1),
                Name = reader.GetString(2),
                SortOrder = reader.GetInt32(3),
                Color = reader.IsDBNull(4) ? null : reader.GetString(4),
                IsRequired = reader.GetInt64(5) != 0
            });
        }
        return result;
    }

    public static void SetMinorCategoryRequired(long id, bool isRequired, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {tbl.Minor} SET IsRequired = $required WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$required", isRequired ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>必須指定されている中カテゴリを全件返す（タグ選択ダイアログでのバリデーション用）。</summary>
    public static List<MinorCategory> GetRequiredMinorCategories(TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Id, MajorCategoryId, Name, SortOrder, Color, IsRequired FROM {tbl.Minor} WHERE IsRequired = 1 ORDER BY SortOrder, Name;";
        using var reader = cmd.ExecuteReader();
        var result = new List<MinorCategory>();
        while (reader.Read())
        {
            result.Add(new MinorCategory
            {
                Id = reader.GetInt64(0),
                MajorCategoryId = reader.GetInt64(1),
                Name = reader.GetString(2),
                SortOrder = reader.GetInt32(3),
                Color = reader.IsDBNull(4) ? null : reader.GetString(4),
                IsRequired = true
            });
        }
        return result;
    }

    public static long AddMinorCategory(long majorCategoryId, string name, int sortOrder = 0, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO {tbl.Minor} (MajorCategoryId, Name, SortOrder) VALUES ($majorId, $name, $sort); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$majorId", majorCategoryId);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$sort", sortOrder);
        return (long)cmd.ExecuteScalar()!;
    }

    public static void SetMinorCategoryColor(long id, string? color, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {tbl.Minor} SET Color = $color WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$color", (object?)color ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public static void RenameMinorCategory(long id, string newName, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {tbl.Minor} SET Name = $name WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$name", newName);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 渡された順序で中カテゴリのSortOrderを振り直す（ドラッグ&ドロップによる並べ替え用）。
    /// </summary>
    public static void SetMinorCategorySortOrders(List<long> orderedIds, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var tx = conn.BeginTransaction();
        for (int i = 0; i < orderedIds.Count; i++)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $"UPDATE {tbl.Minor} SET SortOrder = $sort WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$sort", i);
            cmd.Parameters.AddWithValue("$id", orderedIds[i]);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public static void DeleteMinorCategory(long id, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM {tbl.Minor} WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // === タグ ===

    public static List<Tag> GetAllTags(TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Id, MinorCategoryId, Name, SortOrder, Color FROM {tbl.Tags} ORDER BY SortOrder, Name;";
        using var reader = cmd.ExecuteReader();
        return ReadTags(reader);
    }

    public static List<Tag> GetTagsByMinorCategory(long minorCategoryId, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Id, MinorCategoryId, Name, SortOrder, Color FROM {tbl.Tags} WHERE MinorCategoryId = $minorId ORDER BY SortOrder, Name;";
        cmd.Parameters.AddWithValue("$minorId", minorCategoryId);
        using var reader = cmd.ExecuteReader();
        return ReadTags(reader);
    }

    public static long AddTag(string name, long? minorCategoryId = null, int sortOrder = 0, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO {tbl.Tags} (MinorCategoryId, Name, SortOrder) VALUES ($minorId, $name, $sort); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$minorId", (object?)minorCategoryId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$sort", sortOrder);
        return (long)cmd.ExecuteScalar()!;
    }

    public static void RenameTag(long id, string newName, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {tbl.Tags} SET Name = $name WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$name", newName);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public static void SetTagColor(long id, string? color, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {tbl.Tags} SET Color = $color WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$color", (object?)color ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public static void SetTagCategory(long id, long? minorCategoryId, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {tbl.Tags} SET MinorCategoryId = $minorId WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$minorId", (object?)minorCategoryId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 渡された順序でタグのSortOrderを振り直す（ドラッグ&ドロップによる並べ替え用）。
    /// </summary>
    public static void SetTagSortOrders(List<long> orderedTagIds, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var tx = conn.BeginTransaction();
        for (int i = 0; i < orderedTagIds.Count; i++)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $"UPDATE {tbl.Tags} SET SortOrder = $sort WHERE Id = $id;";
            cmd.Parameters.AddWithValue("$sort", i);
            cmd.Parameters.AddWithValue("$id", orderedTagIds[i]);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public static void DeleteTag(long id, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM {tbl.Tags} WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    private static List<Tag> ReadTags(SqliteDataReader reader)
    {
        var result = new List<Tag>();
        while (reader.Read())
        {
            result.Add(new Tag
            {
                Id = reader.GetInt64(0),
                MinorCategoryId = reader.IsDBNull(1) ? null : reader.GetInt64(1),
                Name = reader.GetString(2),
                SortOrder = reader.GetInt32(3),
                Color = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }
        return result;
    }

    // === ファイルエントリ（パス＋サイズ＋更新日時が識別キー） ===

    public static long GetOrCreateFileEntry(string filePath, long fileSize, long lastModifiedTicks, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();

        using (var select = conn.CreateCommand())
        {
            select.CommandText = $"SELECT Id FROM {tbl.FileEntries} WHERE FilePath = $path AND FileSize = $size AND LastModifiedTicks = $ticks;";
            select.Parameters.AddWithValue("$path", filePath);
            select.Parameters.AddWithValue("$size", fileSize);
            select.Parameters.AddWithValue("$ticks", lastModifiedTicks);
            var existing = select.ExecuteScalar();
            if (existing != null) return (long)existing;
        }

        using var insert = conn.CreateCommand();
        insert.CommandText = $"INSERT INTO {tbl.FileEntries} (FilePath, FileSize, LastModifiedTicks) VALUES ($path, $size, $ticks); SELECT last_insert_rowid();";
        insert.Parameters.AddWithValue("$path", filePath);
        insert.Parameters.AddWithValue("$size", fileSize);
        insert.Parameters.AddWithValue("$ticks", lastModifiedTicks);
        return (long)insert.ExecuteScalar()!;
    }

    public static FileEntry? FindFileEntry(string filePath, long fileSize, long lastModifiedTicks, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Id, FilePath, FileSize, LastModifiedTicks, WorkTitle FROM {tbl.FileEntries} WHERE FilePath = $path AND FileSize = $size AND LastModifiedTicks = $ticks;";
        cmd.Parameters.AddWithValue("$path", filePath);
        cmd.Parameters.AddWithValue("$size", fileSize);
        cmd.Parameters.AddWithValue("$ticks", lastModifiedTicks);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new FileEntry
        {
            Id = reader.GetInt64(0),
            FilePath = reader.GetString(1),
            FileSize = reader.GetInt64(2),
            LastModifiedTicks = reader.GetInt64(3),
            WorkTitle = reader.IsDBNull(4) ? null : reader.GetString(4)
        };
    }

    /// <summary>タグが1つでも付いているFileEntriesの識別キー集合を返す（タグ未設定/設定済み検索用）。</summary>
    public static HashSet<(string Path, long Size, long Ticks)> GetFileEntriesWithAnyTag(TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT DISTINCT fe.FilePath, fe.FileSize, fe.LastModifiedTicks
            FROM {tbl.FileEntries} fe
            JOIN {tbl.FileTags} ft ON ft.FileEntryId = fe.Id;
            """;
        using var reader = cmd.ExecuteReader();
        var result = new HashSet<(string, long, long)>();
        while (reader.Read())
            result.Add((reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2)));
        return result;
    }

    /// <summary>各タグが現在何件のファイルに付与されているかを返す（タグ選択UIでの件数表示用）。</summary>
    public static Dictionary<long, int> GetTagUsageCounts(TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT TagId, COUNT(*) FROM {tbl.FileTags} GROUP BY TagId;";
        using var reader = cmd.ExecuteReader();
        var result = new Dictionary<long, int>();
        while (reader.Read())
            result[reader.GetInt64(0)] = (int)reader.GetInt64(1);
        return result;
    }

    /// <summary>
    /// タグ条件（AND/OR）に一致するFileEntriesを識別キー付きで返す。タグ未指定なら全件を返す。
    /// 呼び出し側でディスク上の実在ファイル一覧と突き合わせて絞り込む想定（タグ検索の基礎データ）。
    /// </summary>
    public static Dictionary<(string Path, long Size, long Ticks), string?> GetFileEntriesMatchingTags(IReadOnlyList<long> tagIds, bool matchAll, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();

        if (tagIds.Count == 0)
        {
            cmd.CommandText = $"SELECT FilePath, FileSize, LastModifiedTicks, WorkTitle FROM {tbl.FileEntries};";
        }
        else
        {
            var placeholders = string.Join(",", tagIds.Select((_, i) => $"$t{i}"));
            cmd.CommandText = matchAll
                ? $"""
                    SELECT fe.FilePath, fe.FileSize, fe.LastModifiedTicks, fe.WorkTitle
                    FROM {tbl.FileEntries} fe
                    JOIN {tbl.FileTags} ft ON ft.FileEntryId = fe.Id
                    WHERE ft.TagId IN ({placeholders})
                    GROUP BY fe.Id
                    HAVING COUNT(DISTINCT ft.TagId) = {tagIds.Count};
                    """
                : $"""
                    SELECT DISTINCT fe.FilePath, fe.FileSize, fe.LastModifiedTicks, fe.WorkTitle
                    FROM {tbl.FileEntries} fe
                    JOIN {tbl.FileTags} ft ON ft.FileEntryId = fe.Id
                    WHERE ft.TagId IN ({placeholders});
                    """;
            for (int i = 0; i < tagIds.Count; i++)
                cmd.Parameters.AddWithValue($"$t{i}", tagIds[i]);
        }

        using var reader = cmd.ExecuteReader();
        var result = new Dictionary<(string, long, long), string?>();
        while (reader.Read())
        {
            var key = (reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2));
            result[key] = reader.IsDBNull(3) ? null : reader.GetString(3);
        }
        return result;
    }

    /// <summary>
    /// ファイル名変更・移動後、既存の識別キー（旧パス＋サイズ＋更新日時）に一致するFileEntriesのFilePathを更新する。
    /// タグ・作品名はそのまま引き継がれる。該当するFileEntriesが無い（未タグ付けファイル）場合は何もしない。
    /// newLastModifiedTicks: 移動時にファイルの更新日時も変更する場合、実際に書き込まれた新しい更新日時を渡す
    /// （渡さなければ変更なしとみなし、識別キーのLastModifiedTicksはそのまま引き継ぐ＝単純なリネーム用）。
    /// </summary>
    public static void UpdateFileEntryPath(string oldPath, long fileSize, long lastModifiedTicks, string newPath,
        TagDomain domain = TagDomain.Image, long? newLastModifiedTicks = null)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {tbl.FileEntries} SET FilePath = $newPath, LastModifiedTicks = $newTicks WHERE FilePath = $oldPath AND FileSize = $size AND LastModifiedTicks = $ticks;";
        cmd.Parameters.AddWithValue("$newPath", newPath);
        cmd.Parameters.AddWithValue("$newTicks", newLastModifiedTicks ?? lastModifiedTicks);
        cmd.Parameters.AddWithValue("$oldPath", oldPath);
        cmd.Parameters.AddWithValue("$size", fileSize);
        cmd.Parameters.AddWithValue("$ticks", lastModifiedTicks);
        cmd.ExecuteNonQuery();
    }

    public static void SetWorkTitle(long fileEntryId, string? workTitle, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {tbl.FileEntries} SET WorkTitle = $title WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$title", (object?)workTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", fileEntryId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 識別キーに一致するFileEntriesを削除する（存在すれば）。FileTagsはON DELETE CASCADEで連動削除される。
    /// 削除フォルダへの移動時など、ファイルがもう存在しないものとして扱う場合に使う。
    /// </summary>
    public static void DeleteFileEntryByIdentity(string filePath, long fileSize, long lastModifiedTicks, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM {tbl.FileEntries} WHERE FilePath = $path AND FileSize = $size AND LastModifiedTicks = $ticks;";
        cmd.Parameters.AddWithValue("$path", filePath);
        cmd.Parameters.AddWithValue("$size", fileSize);
        cmd.Parameters.AddWithValue("$ticks", lastModifiedTicks);
        cmd.ExecuteNonQuery();
    }

    // === ファイル⇔タグ紐付け ===

    public static List<Tag> GetTagsForFile(long fileEntryId, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT t.Id, t.MinorCategoryId, t.Name, t.SortOrder, t.Color
            FROM {tbl.Tags} t
            JOIN {tbl.FileTags} ft ON ft.TagId = t.Id
            LEFT JOIN {tbl.Minor} mc ON mc.Id = t.MinorCategoryId
            LEFT JOIN {tbl.Major} majc ON majc.Id = mc.MajorCategoryId
            WHERE ft.FileEntryId = $fileId
            ORDER BY
                (majc.Id IS NULL), majc.SortOrder, majc.Name,
                (mc.Id IS NULL), mc.SortOrder, mc.Name,
                t.SortOrder, t.Name;
            """;
        cmd.Parameters.AddWithValue("$fileId", fileEntryId);
        using var reader = cmd.ExecuteReader();
        return ReadTags(reader);
    }

    public static void AddFileTag(long fileEntryId, long tagId, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT OR IGNORE INTO {tbl.FileTags} (FileEntryId, TagId) VALUES ($fileId, $tagId);";
        cmd.Parameters.AddWithValue("$fileId", fileEntryId);
        cmd.Parameters.AddWithValue("$tagId", tagId);
        cmd.ExecuteNonQuery();
    }

    public static void RemoveFileTag(long fileEntryId, long tagId, TagDomain domain = TagDomain.Image)
    {
        var tbl = Tables(domain);
        using var conn = TagDatabaseService.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM {tbl.FileTags} WHERE FileEntryId = $fileId AND TagId = $tagId;";
        cmd.Parameters.AddWithValue("$fileId", fileEntryId);
        cmd.Parameters.AddWithValue("$tagId", tagId);
        cmd.ExecuteNonQuery();
    }
}
