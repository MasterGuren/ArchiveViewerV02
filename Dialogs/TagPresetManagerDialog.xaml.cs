using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ArchiveViewer.Models;
using ArchiveViewer.Services;

namespace ArchiveViewer.Dialogs;

/// <summary>
/// タグプリセットの名前変更・タグの編集・削除・並べ替え（ドラッグ&ドロップ）を行う画面。
/// 画像用/動画用でプリセットは別管理なので、domainで操作対象を切り替える。
/// </summary>
public partial class TagPresetManagerDialog : Window
{
    private readonly TagDomain _domain;
    private System.Windows.Point _dragStartPoint;
    private ListBoxItem? _draggedItem;

    /// <summary>「タグを編集」で開くTagPickerDialogのサイズ。呼び出し側が前回サイズを渡し、閉じた後に読み戻して保存する。</summary>
    public double? PickerWidth { get; private set; }
    public double? PickerHeight { get; private set; }

    public TagPresetManagerDialog(TagDomain domain = TagDomain.Image, double? pickerWidth = null, double? pickerHeight = null)
    {
        InitializeComponent();
        _domain = domain;
        PickerWidth = pickerWidth;
        PickerHeight = pickerHeight;
        RefreshPresets();
    }

    private TagPreset? SelectedPreset => (PresetList.SelectedItem as ListBoxItem)?.Tag as TagPreset;

    private static ListBoxItem BuildTagRow(Tag tag)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var swatch = new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = new CornerRadius(3),
            Margin = new Thickness(0, 0, 6, 0),
            BorderThickness = new Thickness(1),
            BorderBrush = Theme.BorderBrush,
            Background = string.IsNullOrEmpty(tag.Color)
                ? Brushes.Transparent
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString(tag.Color)!)
        };
        panel.Children.Add(swatch);
        panel.Children.Add(new TextBlock { Text = tag.Name, VerticalAlignment = VerticalAlignment.Center });
        return new ListBoxItem { Content = panel, Tag = tag };
    }

    private void RefreshPresets(long? selectId = null)
    {
        PresetList.Items.Clear();
        ListBoxItem? toSelect = null;
        foreach (var preset in TagRepository.GetTagPresets(_domain))
        {
            var item = new ListBoxItem
            {
                Content = new TextBlock { Text = $"{preset.Name}（{preset.Tags.Count}個）" },
                Tag = preset
            };
            PresetList.Items.Add(item);
            if (selectId.HasValue && preset.Id == selectId.Value) toSelect = item;
        }
        if (toSelect != null)
            PresetList.SelectedItem = toSelect;
        else
            RefreshTags();
    }

    private void RefreshTags()
    {
        TagList.Items.Clear();
        var preset = SelectedPreset;
        if (preset == null) return;
        if (preset.Tags.Count == 0)
        {
            TagList.Items.Add(new ListBoxItem { Content = "（タグがすべて削除されています）", IsEnabled = false });
            return;
        }
        foreach (var tag in preset.Tags)
            TagList.Items.Add(BuildTagRow(tag));
    }

    private void PresetList_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshTags();

    private void PresetList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) == null) return;
        EditSelectedPresetTags();
    }

    private void BtnRename_Click(object sender, RoutedEventArgs e)
    {
        var preset = SelectedPreset;
        if (preset == null) return;
        var dlg = new InputDialog("プリセット名変更", "新しい名前:", preset.Name) { Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.InputText)) return;
        var name = dlg.InputText.Trim();
        if (name == preset.Name) return;
        if (TagRepository.GetTagPresets(_domain).Any(p => p.Name == name))
        {
            MessageBox.Show($"「{name}」は既に存在します。", "プリセット名変更");
            return;
        }
        TagRepository.RenameTagPreset(preset.Id, name, _domain);
        RefreshPresets(preset.Id);
    }

    private void BtnEditTags_Click(object sender, RoutedEventArgs e) => EditSelectedPresetTags();

    /// <summary>タグ付けと同じTagPickerDialogでプリセットの中身を選び直す。空にはできない（不要なら削除してもらう）。</summary>
    private void EditSelectedPresetTags()
    {
        var preset = SelectedPreset;
        if (preset == null) return;
        var dlg = new TagPickerDialog(preset.Tags.Select(t => t.Id), enforceRequiredCategories: false,
            PickerWidth, PickerHeight, _domain) { Owner = this };
        var result = dlg.ShowDialog();
        PickerWidth = dlg.Width;
        PickerHeight = dlg.Height;
        if (result != true) return;
        if (dlg.SelectedTagIds.Count == 0)
        {
            MessageBox.Show("タグを1つ以上選んでください。\n（プリセットが不要な場合は「削除」してください）", "タグを編集");
            return;
        }
        TagRepository.SetTagPresetTags(preset.Id, dlg.SelectedTagIds, _domain);
        RefreshPresets(preset.Id);
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        var preset = SelectedPreset;
        if (preset == null) return;
        if (MessageBox.Show($"プリセット「{preset.Name}」を削除しますか？\n（ファイルに付いているタグはそのままです）", "確認", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;
        TagRepository.DeleteTagPreset(preset.Id, _domain);
        RefreshPresets();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    // === ドラッグ&ドロップ並べ替え ===

    private void PresetList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _draggedItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
    }

    private void PresetList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedItem == null) return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var item = _draggedItem;
        _draggedItem = null;
        DragDrop.DoDragDrop(item, item, DragDropEffects.Move);
    }

    private void PresetList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ListBoxItem)) is not ListBoxItem sourceItem) return;
        var targetItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (targetItem == null || ReferenceEquals(targetItem, sourceItem)) return;
        if (sourceItem.Tag is not TagPreset source || targetItem.Tag is not TagPreset target) return;

        var orderedIds = TagRepository.GetTagPresets(_domain).Select(p => p.Id).ToList();
        var oldIndex = orderedIds.IndexOf(source.Id);
        var newIndex = orderedIds.IndexOf(target.Id);
        if (oldIndex < 0 || newIndex < 0) return;

        orderedIds.RemoveAt(oldIndex);
        orderedIds.Insert(newIndex, source.Id);

        TagRepository.SetTagPresetSortOrders(orderedIds, _domain);
        RefreshPresets(source.Id);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
