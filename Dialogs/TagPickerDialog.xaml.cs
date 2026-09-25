using System.Windows;
using System.Windows.Controls;
using ArchiveViewer.Models;
using ArchiveViewer.Services;

namespace ArchiveViewer.Dialogs;

public partial class TagPickerDialog : Window
{
    public HashSet<long> SelectedTagIds { get; }
    private readonly bool _enforceRequiredCategories;
    private readonly TagDomain _domain;

    /// <summary>
    /// enforceRequiredCategories: trueの場合、必須指定された中カテゴリごとに最低1つタグを選ぶまでOKボタンを押せない。
    /// ファイルへのタグ付与では強制し、検索の絞り込み用ピッカーでは強制しない（部分的な絞り込みを妨げないため）。
    /// initialWidth/initialHeight: 前回リサイズしたサイズを呼び出し側から復元するための初期値（省略時はXAMLの既定値）。
    /// domain: 画像用タグ/動画用タグのどちらを操作するか。
    /// </summary>
    public TagPickerDialog(IEnumerable<long> initiallySelected, bool enforceRequiredCategories = true,
        double? initialWidth = null, double? initialHeight = null, TagDomain domain = TagDomain.Image)
    {
        InitializeComponent();
        SelectedTagIds = [.. initiallySelected];
        _enforceRequiredCategories = enforceRequiredCategories;
        _domain = domain;
        if (initialWidth.HasValue) Width = initialWidth.Value;
        if (initialHeight.HasValue) Height = initialHeight.Value;
        RefreshList();
        // 初回はActualHeightがまだ0のため概算で組んでいる。実レイアウト確定後に高さに基づいて組み直す。
        Loaded += (_, _) => RefreshList();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => RefreshList();

    /// <summary>この画面は縦ではなく横方向に並ぶ列でスクロールするので、上下ホイールをそのまま横スクロールに割り当てる。</summary>
    private void ContentScroller_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        ContentScroller.ScrollToHorizontalOffset(ContentScroller.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private const double ColumnWidth = 190;
    private const double RowHeight = 21;
    private const double FallbackColumnHeight = 480;

    private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e) => RefreshList();

    /// <summary>
    /// タグ名・大カテゴリ名・中カテゴリ名のいずれかに絞り込み文字列が含まれるか判定する。
    /// 大/中カテゴリ名がヒットした場合はその配下のタグを全部表示する（カテゴリ単位での絞り込み）。
    /// </summary>
    private static bool Matches(string text, string filter) => text.Contains(filter, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 大カテゴリごとに1列を固定すると、タグが少ないカテゴリだけの列に余白ができてしまうので、
    /// 見出し・タグ行をすべてフラットな行の列として並べ、列の目標行数に達したら次の列へ、
    /// という形で詰めていく（カテゴリの途中で列をまたいでもよい前提）。読み順は保ったまま隙間を減らす。
    /// </summary>
    private void RefreshList()
    {
        ColumnsPanel.Children.Clear();
        var usageCounts = TagRepository.GetTagUsageCounts(_domain);
        var filter = TxtFilter.Text.Trim();
        var hasFilter = !string.IsNullOrEmpty(filter);

        var rows = new List<UIElement>();

        foreach (var major in TagRepository.GetMajorCategories(_domain))
        {
            var majorMatches = !hasFilter || Matches(major.Name, filter);
            var majorHeaderAdded = false;

            foreach (var minor in TagRepository.GetMinorCategories(major.Id, _domain))
            {
                var minorMatches = majorMatches || Matches(minor.Name, filter);
                var tags = TagRepository.GetTagsByMinorCategory(minor.Id, _domain);
                var visibleTags = minorMatches ? tags : tags.Where(t => Matches(t.Name, filter)).ToList();
                if (visibleTags.Count == 0) continue;

                if (!majorHeaderAdded)
                {
                    rows.Add(new TextBlock
                    {
                        Text = major.Name,
                        Foreground = Theme.TextBrush,
                        FontFamily = new FontFamily(Theme.FontFamily),
                        FontSize = 15,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 0, 4)
                    });
                    majorHeaderAdded = true;
                }

                rows.Add(new TextBlock
                {
                    Text = minor.Name,
                    Foreground = Theme.SubtextBrush,
                    FontFamily = new FontFamily(Theme.FontFamily),
                    FontSize = 13,
                    Margin = new Thickness(0, 4, 0, 1)
                });

                foreach (var tag in visibleTags)
                    rows.Add(BuildTagRow(tag, 8, usageCounts.GetValueOrDefault(tag.Id)));
            }
        }

        var uncategorized = TagRepository.GetAllTags(_domain).Where(t => t.MinorCategoryId == null).ToList();
        var visibleUncategorized = hasFilter ? uncategorized.Where(t => Matches(t.Name, filter)).ToList() : uncategorized;
        if (visibleUncategorized.Count > 0)
        {
            rows.Add(new TextBlock
            {
                Text = "未分類",
                Foreground = Theme.TextBrush,
                FontFamily = new FontFamily(Theme.FontFamily),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            });
            foreach (var tag in visibleUncategorized)
                rows.Add(BuildTagRow(tag, 0, usageCounts.GetValueOrDefault(tag.Id)));
        }

        if (hasFilter && rows.Count == 0)
        {
            ColumnsPanel.Children.Add(new TextBlock
            {
                Text = "一致するタグ・カテゴリがありません",
                Foreground = Theme.SubtextBrush,
                FontFamily = new FontFamily(Theme.FontFamily),
                FontSize = 13
            });
            UpdateOkButtonState();
            return;
        }

        // 表示領域の高さいっぱいまで詰めたら次の列へ改行する（列数は揃えない）。
        var availableHeight = ContentScroller.ActualHeight > 0 ? ContentScroller.ActualHeight : FallbackColumnHeight;
        var rowsPerColumn = Math.Max(1, (int)(availableHeight / RowHeight));

        StackPanel NewColumn()
        {
            var c = new StackPanel { Width = ColumnWidth, Margin = new Thickness(0, 0, 12, 0) };
            ColumnsPanel.Children.Add(c);
            return c;
        }

        var column = NewColumn();
        var colCount = 0;
        foreach (var row in rows)
        {
            if (colCount >= rowsPerColumn)
            {
                column = NewColumn();
                colCount = 0;
            }
            column.Children.Add(row);
            colCount++;
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
        foreach (var minor in TagRepository.GetRequiredMinorCategories(_domain))
        {
            var tagIdsInMinor = TagRepository.GetTagsByMinorCategory(minor.Id, _domain).Select(t => t.Id).ToHashSet();
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
            Margin = new Thickness(indent, 1, 0, 0),
            IsChecked = SelectedTagIds.Contains(tag.Id),
            Tag = tag.Id
        };
        checkBox.Checked += (_, _) => { SelectedTagIds.Add(tag.Id); UpdateOkButtonState(); };
        checkBox.Unchecked += (_, _) => { SelectedTagIds.Remove(tag.Id); UpdateOkButtonState(); };
        return checkBox;
    }

    private void BtnOpenTagManager_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new TagManagerDialog(_domain) { Owner = this };
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
