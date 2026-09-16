using System.Windows;
using System.Windows.Controls;
using ArchiveViewer.Models;
using ArchiveViewer.Services;

namespace ArchiveViewer.Dialogs;

public partial class TagPickerDialog : Window
{
    public HashSet<long> SelectedTagIds { get; }
    private readonly bool _enforceRequiredCategories;

    /// <summary>
    /// enforceRequiredCategories: trueの場合、必須指定された中カテゴリごとに最低1つタグを選ぶまでOKボタンを押せない。
    /// ファイルへのタグ付与では強制し、検索の絞り込み用ピッカーでは強制しない（部分的な絞り込みを妨げないため）。
    /// </summary>
    public TagPickerDialog(IEnumerable<long> initiallySelected, bool enforceRequiredCategories = true)
    {
        InitializeComponent();
        SelectedTagIds = [.. initiallySelected];
        _enforceRequiredCategories = enforceRequiredCategories;
        RefreshList();
    }

    private const double ColumnWidth = 220;

    private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e) => RefreshList();

    /// <summary>
    /// タグ名・大カテゴリ名・中カテゴリ名のいずれかに絞り込み文字列が含まれるか判定する。
    /// 大/中カテゴリ名がヒットした場合はその配下のタグを全部表示する（カテゴリ単位での絞り込み）。
    /// </summary>
    private static bool Matches(string text, string filter) => text.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private void RefreshList()
    {
        ColumnsPanel.Children.Clear();
        var usageCounts = TagRepository.GetTagUsageCounts();
        var filter = TxtFilter.Text.Trim();
        var hasFilter = !string.IsNullOrEmpty(filter);

        foreach (var major in TagRepository.GetMajorCategories())
        {
            var majorMatches = !hasFilter || Matches(major.Name, filter);
            var column = new StackPanel { Width = ColumnWidth, Margin = new Thickness(0, 0, 16, 0) };
            var columnHasContent = false;

            foreach (var minor in TagRepository.GetMinorCategories(major.Id))
            {
                var minorMatches = majorMatches || Matches(minor.Name, filter);
                var tags = TagRepository.GetTagsByMinorCategory(minor.Id);
                var visibleTags = minorMatches ? tags : tags.Where(t => Matches(t.Name, filter)).ToList();
                if (visibleTags.Count == 0) continue;

                if (!columnHasContent)
                {
                    column.Children.Add(new TextBlock
                    {
                        Text = major.Name,
                        Foreground = Theme.TextBrush,
                        FontFamily = new FontFamily(Theme.FontFamily),
                        FontSize = 15,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 0, 6)
                    });
                    columnHasContent = true;
                }

                column.Children.Add(new TextBlock
                {
                    Text = minor.Name,
                    Foreground = Theme.SubtextBrush,
                    FontFamily = new FontFamily(Theme.FontFamily),
                    FontSize = 13,
                    Margin = new Thickness(0, 6, 0, 2)
                });

                foreach (var tag in visibleTags)
                    column.Children.Add(BuildTagRow(tag, 12, usageCounts.GetValueOrDefault(tag.Id)));
            }

            if (columnHasContent)
                ColumnsPanel.Children.Add(column);
        }

        var uncategorized = TagRepository.GetAllTags().Where(t => t.MinorCategoryId == null).ToList();
        var visibleUncategorized = hasFilter ? uncategorized.Where(t => Matches(t.Name, filter)).ToList() : uncategorized;
        if (visibleUncategorized.Count > 0)
        {
            var column = new StackPanel { Width = ColumnWidth, Margin = new Thickness(0, 0, 16, 0) };
            column.Children.Add(new TextBlock
            {
                Text = "未分類",
                Foreground = Theme.TextBrush,
                FontFamily = new FontFamily(Theme.FontFamily),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6)
            });
            foreach (var tag in visibleUncategorized)
                column.Children.Add(BuildTagRow(tag, 0, usageCounts.GetValueOrDefault(tag.Id)));
            ColumnsPanel.Children.Add(column);
        }

        if (hasFilter && ColumnsPanel.Children.Count == 0)
        {
            ColumnsPanel.Children.Add(new TextBlock
            {
                Text = "一致するタグ・カテゴリがありません",
                Foreground = Theme.SubtextBrush,
                FontFamily = new FontFamily(Theme.FontFamily),
                FontSize = 13
            });
        }

        UpdateOkButtonState();
    }

    /// <summary>必須指定された中カテゴリすべてから最低1つタグが選ばれているか確認し、OKボタンの有効/無効を切り替える。</summary>
    private void UpdateOkButtonState()
    {
        if (!_enforceRequiredCategories)
        {
            BtnOk.IsEnabled = true;
            TxtRequiredWarning.Visibility = Visibility.Collapsed;
            return;
        }

        var missing = new List<string>();
        foreach (var minor in TagRepository.GetRequiredMinorCategories())
        {
            var tagIdsInMinor = TagRepository.GetTagsByMinorCategory(minor.Id).Select(t => t.Id).ToHashSet();
            if (!tagIdsInMinor.Overlaps(SelectedTagIds))
                missing.Add(minor.Name);
        }

        BtnOk.IsEnabled = missing.Count == 0;
        if (missing.Count > 0)
        {
            TxtRequiredWarning.Text = $"必須カテゴリから最低1つ選んでください: {string.Join("、", missing)}";
            TxtRequiredWarning.Visibility = Visibility.Visible;
        }
        else
        {
            TxtRequiredWarning.Visibility = Visibility.Collapsed;
        }
    }

    private System.Windows.Controls.CheckBox BuildTagRow(Tag tag, double indent, int usageCount)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var swatch = new Border
        {
            Width = 12,
            Height = 12,
            CornerRadius = new CornerRadius(3),
            Margin = new Thickness(0, 0, 6, 0),
            BorderThickness = new Thickness(1),
            BorderBrush = Theme.BorderBrush,
            Background = string.IsNullOrEmpty(tag.Color)
                ? Brushes.Transparent
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString(tag.Color)!)
        };
        panel.Children.Add(swatch);
        panel.Children.Add(new TextBlock { Text = tag.Name, Foreground = Theme.TextBrush, FontFamily = new FontFamily(Theme.FontFamily), FontSize = 13 });
        panel.Children.Add(new TextBlock
        {
            Text = $" ({usageCount})",
            Foreground = Theme.SubtextBrush,
            FontFamily = new FontFamily(Theme.FontFamily),
            FontSize = 12
        });

        var checkBox = new System.Windows.Controls.CheckBox
        {
            Content = panel,
            Foreground = Theme.TextBrush,
            Margin = new Thickness(indent, 2, 0, 0),
            IsChecked = SelectedTagIds.Contains(tag.Id),
            Tag = tag.Id
        };
        checkBox.Checked += (_, _) => { SelectedTagIds.Add(tag.Id); UpdateOkButtonState(); };
        checkBox.Unchecked += (_, _) => { SelectedTagIds.Remove(tag.Id); UpdateOkButtonState(); };
        return checkBox;
    }

    private void BtnOpenTagManager_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new TagManagerDialog { Owner = this };
        dlg.ShowDialog();
        RefreshList();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
