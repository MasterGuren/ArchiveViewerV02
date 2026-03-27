using ArchiveViewer.Models;

namespace ArchiveViewer.Services;

public enum RatingAction
{
    Shootdown,      // +3
    Promote2,       // +2
    Promote1,       // +1
    Stay,           // → confirmed folder
    Demote1,        // -1
    Delete,         // → delete folder
    CategoryMove    // → category move folder (0→1 only)
}

public static class RatingService
{
    public static readonly List<(string Label, string Color, RatingAction Action)> Buttons =
    [
        ("★撃墜",   "#ffffff", RatingAction.Shootdown),
        ("▲▲2昇格", "#ef4444", RatingAction.Promote2),
        ("▲昇格",   "#e6f020", RatingAction.Promote1),
        ("▶ステイ",  "#22c55e", RatingAction.Stay),
        ("▶▲カテゴリー移動", "#8b5cf6", RatingAction.CategoryMove),
        ("▼降格",   "#3b82f6", RatingAction.Demote1),
        ("✖削除",   "#7f1d1d", RatingAction.Delete),
    ];

    /// <summary>
    /// Returns the destination folder for the given action, or null if not applicable.
    /// </summary>
    public static string? GetTargetFolder(RatingPresetData preset, int currentRank, RatingAction action)
    {
        return action switch
        {
            RatingAction.Shootdown => GetRatingFolder(preset, Math.Min(currentRank + 3, 9)),
            RatingAction.Promote2 => GetRatingFolder(preset, Math.Min(currentRank + 2, 9)),
            RatingAction.Promote1 => GetRatingFolder(preset, Math.Min(currentRank + 1, 9)),
            RatingAction.Stay => GetConfirmedFolder(preset, currentRank),
            RatingAction.CategoryMove => currentRank == 0 ? GetCategoryMoveFolder(preset) : null,
            RatingAction.Demote1 => currentRank <= 0 ? null : GetRatingFolder(preset, currentRank - 1),
            RatingAction.Delete => GetDeleteFolder(preset, currentRank),
            _ => null
        };
    }

    /// <summary>
    /// Returns a description like "+3 → ★9" for the action at the given rank.
    /// </summary>
    public static string? GetTargetDescription(int currentRank, RatingAction action)
    {
        return action switch
        {
            RatingAction.Shootdown => $"+3 → ★{Math.Min(currentRank + 3, 9)}",
            RatingAction.Promote2 => $"+2 → ★{Math.Min(currentRank + 2, 9)}",
            RatingAction.Promote1 => $"+1 → ★{Math.Min(currentRank + 1, 9)}",
            RatingAction.Stay => $"→ ★{currentRank}確",
            RatingAction.CategoryMove => currentRank == 0 ? "→ カテゴリー" : null,
            RatingAction.Demote1 => currentRank <= 0 ? null : $"-1 → ★{currentRank - 1}",
            RatingAction.Delete => "→ 削除",
            _ => null
        };
    }

    private static string? GetRatingFolder(RatingPresetData preset, int rank)
    {
        if (rank < 0 || rank >= preset.RatingFolders.Count) return null;
        var folder = preset.RatingFolders[rank];
        return string.IsNullOrEmpty(folder) ? null : folder;
    }

    private static string? GetConfirmedFolder(RatingPresetData preset, int rank)
    {
        if (rank < 0 || rank >= preset.ConfirmedFolders.Count) return null;
        var folder = preset.ConfirmedFolders[rank];
        return string.IsNullOrEmpty(folder) ? null : folder;
    }

    private static string? GetDeleteFolder(RatingPresetData preset, int rank)
    {
        if (rank < 0 || rank >= preset.DeleteFolders.Count) return null;
        var folder = preset.DeleteFolders[rank];
        return string.IsNullOrEmpty(folder) ? null : folder;
    }

    private static string? GetCategoryMoveFolder(RatingPresetData preset)
    {
        return string.IsNullOrEmpty(preset.CategoryMoveFolder) ? null : preset.CategoryMoveFolder;
    }
}
