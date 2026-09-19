using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ArchiveViewer.Models;
using ArchiveViewer.Services;

namespace ArchiveViewer.Dialogs;

public partial class TagManagerDialog : Window
{
    private readonly TagDomain _domain;
    private System.Windows.Point _dragStartPoint;
    private ListBoxItem? _draggedItem;

    public TagManagerDialog(TagDomain domain = TagDomain.Image)
    {
        InitializeComponent();
        _domain = domain;
        RefreshMajor();
    }

    private MajorCategory? SelectedMajor => (MajorList.SelectedItem as ListBoxItem)?.Tag as MajorCategory;
    private MinorCategory? SelectedMinor => (MinorList.SelectedItem as ListBoxItem)?.Tag as MinorCategory;
    private Tag? SelectedTag => (TagList.SelectedItem as ListBoxItem)?.Tag as Tag;

    private static ListBoxItem BuildRow(string name, string? colorHex, object tag)
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
            Background = string.IsNullOrEmpty(colorHex)
                ? Brushes.Transparent
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)!)
        };
        panel.Children.Add(swatch);
        panel.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
        return new ListBoxItem { Content = panel, Tag = tag };
    }

    /// <summary>新規追加時に末尾（既存の最大SortOrder+1）へ入るようにする。</summary>
    private static int NextSortOrder<T>(IReadOnlyCollection<T> items, Func<T, int> selector) =>
        items.Count == 0 ? 0 : items.Max(selector) + 1;

    private void RefreshMajor(long? selectId = null)
    {
        var list = TagRepository.GetMajorCategories(_domain);
        MajorList.Items.Clear();
        ListBoxItem? toSelect = null;
        foreach (var cat in list)
        {
            var item = BuildRow(cat.Name, cat.Color, cat);
            MajorList.Items.Add(item);
            if (selectId.HasValue && cat.Id == selectId.Value) toSelect = item;
        }
        if (toSelect != null)
            MajorList.SelectedItem = toSelect;
        else
            RefreshMinor();
    }

    private void RefreshMinor(long? selectId = null)
    {
        var major = SelectedMajor;
        MinorList.Items.Clear();
        if (major == null)
        {
            RefreshTags();
            return;
        }
        var list = TagRepository.GetMinorCategories(major.Id, _domain);
        ListBoxItem? toSelect = null;
        foreach (var cat in list)
        {
            var item = BuildRow(cat.IsRequired ? $"⭐ {cat.Name}" : cat.Name, cat.Color, cat);
            MinorList.Items.Add(item);
            if (selectId.HasValue && cat.Id == selectId.Value) toSelect = item;
        }
        if (toSelect != null)
            MinorList.SelectedItem = toSelect;
        else
            RefreshTags();
    }

    private void RefreshTags(long? selectId = null)
    {
        var minor = SelectedMinor;
        TagList.Items.Clear();
        if (minor == null) return;
        var list = TagRepository.GetTagsByMinorCategory(minor.Id, _domain);
        ListBoxItem? toSelect = null;
        foreach (var tag in list)
        {
            var item = BuildRow(tag.Name, tag.Color, tag);
            TagList.Items.Add(item);
            if (selectId.HasValue && tag.Id == selectId.Value) toSelect = item;
        }
        if (toSelect != null)
            TagList.SelectedItem = toSelect;
    }

    private void MajorList_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshMinor();
    private void MinorList_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshTags();

    // === 大カテゴリ ===

    private void BtnNewMajor_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new InputDialog("大カテゴリ追加", "カテゴリ名:") { Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.InputText)) return;
        var name = dlg.InputText.Trim();
        var existing = TagRepository.GetMajorCategories(_domain);
        if (existing.Any(c => c.Name == name))
        {
            MessageBox.Show($"「{name}」は既に存在します。", "大カテゴリ追加");
            return;
        }
        var id = TagRepository.AddMajorCategory(name, NextSortOrder(existing, c => c.SortOrder), _domain);
        RefreshMajor(id);
    }

    /// <summary>Enterのたびに即追加してテキストボックスをクリア。ダイアログを挟まず連続入力できるようにする。</summary>
    private void TxtQuickAddMajor_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;

        var name = TxtQuickAddMajor.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;

        var existing = TagRepository.GetMajorCategories(_domain);
        if (existing.Any(c => c.Name == name))
        {
            MessageBox.Show($"「{name}」は既に存在します。", "大カテゴリ追加");
            return;
        }

        var keepSelected = SelectedMajor?.Id;
        TagRepository.AddMajorCategory(name, NextSortOrder(existing, c => c.SortOrder), _domain);
        TxtQuickAddMajor.Clear();
        RefreshMajor(keepSelected);
        TxtQuickAddMajor.Focus();
    }

    private void BtnRenameMajor_Click(object sender, RoutedEventArgs e)
    {
        var major = SelectedMajor;
        if (major == null) return;
        var dlg = new InputDialog("大カテゴリ名変更", "新しい名前:", major.Name) { Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.InputText)) return;
        var name = dlg.InputText.Trim();
        if (name == major.Name) return;
        if (TagRepository.GetMajorCategories(_domain).Any(c => c.Name == name))
        {
            MessageBox.Show($"「{name}」は既に存在します。", "大カテゴリ名変更");
            return;
        }
        TagRepository.RenameMajorCategory(major.Id, name, _domain);
        RefreshMajor(major.Id);
    }

    private void BtnColorMajor_Click(object sender, RoutedEventArgs e)
    {
        var major = SelectedMajor;
        if (major == null) return;
        var dlg = new ColorPickerDialog(major.Color ?? Theme.ColorChoices[0]) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        TagRepository.SetMajorCategoryColor(major.Id, dlg.SelectedColor, _domain);
        RefreshMajor(major.Id);
    }

    private void BtnDeleteMajor_Click(object sender, RoutedEventArgs e)
    {
        var major = SelectedMajor;
        if (major == null) return;
        if (MessageBox.Show($"大カテゴリ「{major.Name}」を削除しますか？\n（配下の中カテゴリも削除され、タグは未分類になります）", "確認", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;
        TagRepository.DeleteMajorCategory(major.Id, _domain);
        RefreshMajor();
    }

    // === 中カテゴリ ===

    private void BtnNewMinor_Click(object sender, RoutedEventArgs e)
    {
        var major = SelectedMajor;
        if (major == null)
        {
            MessageBox.Show("先に大カテゴリを選択してください。", "中カテゴリ追加");
            return;
        }
        var dlg = new InputDialog("中カテゴリ追加", "カテゴリ名:") { Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.InputText)) return;
        var name = dlg.InputText.Trim();
        var existing = TagRepository.GetMinorCategories(major.Id, _domain);
        if (existing.Any(c => c.Name == name))
        {
            MessageBox.Show($"「{name}」は既に存在します。", "中カテゴリ追加");
            return;
        }
        var id = TagRepository.AddMinorCategory(major.Id, name, NextSortOrder(existing, c => c.SortOrder), _domain);
        if (!string.IsNullOrEmpty(major.Color))
            TagRepository.SetMinorCategoryColor(id, major.Color, _domain);
        RefreshMinor(id);
    }

    /// <summary>Enterのたびに即追加してテキストボックスをクリア。ダイアログを挟まず連続入力できるようにする。</summary>
    private void TxtQuickAddMinor_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;

        var major = SelectedMajor;
        if (major == null)
        {
            MessageBox.Show("先に大カテゴリを選択してください。", "中カテゴリ追加");
            return;
        }

        var name = TxtQuickAddMinor.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;

        var existing = TagRepository.GetMinorCategories(major.Id, _domain);
        if (existing.Any(c => c.Name == name))
        {
            MessageBox.Show($"「{name}」は既に存在します。", "中カテゴリ追加");
            return;
        }

        var keepSelected = SelectedMinor?.Id;
        var id = TagRepository.AddMinorCategory(major.Id, name, NextSortOrder(existing, c => c.SortOrder), _domain);
        if (!string.IsNullOrEmpty(major.Color))
            TagRepository.SetMinorCategoryColor(id, major.Color, _domain);

        TxtQuickAddMinor.Clear();
        RefreshMinor(keepSelected);
        TxtQuickAddMinor.Focus();
    }

    private void BtnRenameMinor_Click(object sender, RoutedEventArgs e)
    {
        var minor = SelectedMinor;
        if (minor == null) return;
        var dlg = new InputDialog("中カテゴリ名変更", "新しい名前:", minor.Name) { Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.InputText)) return;
        var name = dlg.InputText.Trim();
        if (name == minor.Name) return;
        if (TagRepository.GetMinorCategories(minor.MajorCategoryId, _domain).Any(c => c.Name == name))
        {
            MessageBox.Show($"「{name}」は既に存在します。", "中カテゴリ名変更");
            return;
        }
        TagRepository.RenameMinorCategory(minor.Id, name, _domain);
        RefreshMinor(minor.Id);
    }

    private void BtnColorMinor_Click(object sender, RoutedEventArgs e)
    {
        var minor = SelectedMinor;
        if (minor == null) return;
        var dlg = new ColorPickerDialog(minor.Color ?? Theme.ColorChoices[0]) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        TagRepository.SetMinorCategoryColor(minor.Id, dlg.SelectedColor, _domain);
        RefreshMinor(minor.Id);
    }

    private void BtnToggleMinorRequired_Click(object sender, RoutedEventArgs e)
    {
        var minor = SelectedMinor;
        if (minor == null) return;
        TagRepository.SetMinorCategoryRequired(minor.Id, !minor.IsRequired, _domain);
        RefreshMinor(minor.Id);
    }

    private void BtnDeleteMinor_Click(object sender, RoutedEventArgs e)
    {
        var minor = SelectedMinor;
        if (minor == null) return;
        if (MessageBox.Show($"中カテゴリ「{minor.Name}」を削除しますか？\n（配下のタグは未分類になります）", "確認", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;
        TagRepository.DeleteMinorCategory(minor.Id, _domain);
        RefreshMinor();
    }

    // === タグ ===

    private void BtnNewTag_Click(object sender, RoutedEventArgs e)
    {
        var minor = SelectedMinor;
        if (minor == null)
        {
            MessageBox.Show("先に中カテゴリを選択してください。", "タグ追加");
            return;
        }
        var dlg = new InputDialog("タグ追加", "タグ名:") { Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.InputText)) return;
        var name = dlg.InputText.Trim();
        if (TagRepository.GetAllTags(_domain).Any(t => t.Name == name))
        {
            MessageBox.Show($"「{name}」は既に存在します。", "タグ追加");
            return;
        }
        var existing = TagRepository.GetTagsByMinorCategory(minor.Id, _domain);
        var id = TagRepository.AddTag(name, minor.Id, NextSortOrder(existing, t => t.SortOrder), _domain);
        if (!string.IsNullOrEmpty(minor.Color))
            TagRepository.SetTagColor(id, minor.Color, _domain);
        RefreshTags(id);
    }

    /// <summary>Enterのたびに即追加してテキストボックスをクリア。ダイアログを挟まず連続入力できるようにする。</summary>
    private void TxtQuickAddTag_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;

        var minor = SelectedMinor;
        if (minor == null)
        {
            MessageBox.Show("先に中カテゴリを選択してください。", "タグ追加");
            return;
        }

        var name = TxtQuickAddTag.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;

        if (TagRepository.GetAllTags(_domain).Any(t => t.Name == name))
        {
            MessageBox.Show($"「{name}」は既に存在します。", "タグ追加");
            return;
        }

        var existing = TagRepository.GetTagsByMinorCategory(minor.Id, _domain);
        var id = TagRepository.AddTag(name, minor.Id, NextSortOrder(existing, t => t.SortOrder), _domain);
        if (!string.IsNullOrEmpty(minor.Color))
            TagRepository.SetTagColor(id, minor.Color, _domain);

        TxtQuickAddTag.Clear();
        RefreshTags();
        TxtQuickAddTag.Focus();
    }

    private void BtnRenameTag_Click(object sender, RoutedEventArgs e)
    {
        var tag = SelectedTag;
        if (tag == null) return;
        var dlg = new InputDialog("タグ名変更", "新しい名前:", tag.Name) { Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.InputText)) return;
        var name = dlg.InputText.Trim();
        if (name == tag.Name) return;
        if (TagRepository.GetAllTags(_domain).Any(t => t.Name == name))
        {
            MessageBox.Show($"「{name}」は既に存在します。", "タグ名変更");
            return;
        }
        TagRepository.RenameTag(tag.Id, name, _domain);
        RefreshTags(tag.Id);
    }

    private void BtnColorTag_Click(object sender, RoutedEventArgs e)
    {
        var tag = SelectedTag;
        if (tag == null) return;
        var dlg = new ColorPickerDialog(tag.Color ?? Theme.ColorChoices[0]) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        TagRepository.SetTagColor(tag.Id, dlg.SelectedColor, _domain);
        RefreshTags(tag.Id);
    }

    private void BtnDeleteTag_Click(object sender, RoutedEventArgs e)
    {
        var tag = SelectedTag;
        if (tag == null) return;
        if (MessageBox.Show($"タグ「{tag.Name}」を削除しますか？\n（付与済みのファイルからも除去されます）", "確認", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;
        TagRepository.DeleteTag(tag.Id, _domain);
        RefreshTags();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    // === ドラッグ&ドロップ並べ替え（大カテゴリ・中カテゴリ・タグ共通） ===

    private void ListItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _draggedItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
    }

    private void ListItem_PreviewMouseMove(object sender, MouseEventArgs e)
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

    private void MajorList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ListBoxItem)) is not ListBoxItem sourceItem) return;
        var targetItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (targetItem == null || ReferenceEquals(targetItem, sourceItem)) return;
        if (sourceItem.Tag is not MajorCategory sourceCat || targetItem.Tag is not MajorCategory targetCat) return;

        var orderedIds = TagRepository.GetMajorCategories(_domain).Select(c => c.Id).ToList();
        var oldIndex = orderedIds.IndexOf(sourceCat.Id);
        var newIndex = orderedIds.IndexOf(targetCat.Id);
        if (oldIndex < 0 || newIndex < 0) return;

        orderedIds.RemoveAt(oldIndex);
        orderedIds.Insert(newIndex, sourceCat.Id);

        TagRepository.SetMajorCategorySortOrders(orderedIds, _domain);
        RefreshMajor(sourceCat.Id);
    }

    private void MinorList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ListBoxItem)) is not ListBoxItem sourceItem) return;
        var targetItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (targetItem == null || ReferenceEquals(targetItem, sourceItem)) return;
        if (sourceItem.Tag is not MinorCategory sourceCat || targetItem.Tag is not MinorCategory targetCat) return;

        var major = SelectedMajor;
        if (major == null) return;

        var orderedIds = TagRepository.GetMinorCategories(major.Id, _domain).Select(c => c.Id).ToList();
        var oldIndex = orderedIds.IndexOf(sourceCat.Id);
        var newIndex = orderedIds.IndexOf(targetCat.Id);
        if (oldIndex < 0 || newIndex < 0) return;

        orderedIds.RemoveAt(oldIndex);
        orderedIds.Insert(newIndex, sourceCat.Id);

        TagRepository.SetMinorCategorySortOrders(orderedIds, _domain);
        RefreshMinor(sourceCat.Id);
    }

    private void TagList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ListBoxItem)) is not ListBoxItem sourceItem) return;
        var targetItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (targetItem == null || ReferenceEquals(targetItem, sourceItem)) return;
        if (sourceItem.Tag is not Tag sourceTag || targetItem.Tag is not Tag targetTag) return;

        var minor = SelectedMinor;
        if (minor == null) return;

        var orderedIds = TagRepository.GetTagsByMinorCategory(minor.Id, _domain).Select(t => t.Id).ToList();
        var oldIndex = orderedIds.IndexOf(sourceTag.Id);
        var newIndex = orderedIds.IndexOf(targetTag.Id);
        if (oldIndex < 0 || newIndex < 0) return;

        orderedIds.RemoveAt(oldIndex);
        orderedIds.Insert(newIndex, sourceTag.Id);

        TagRepository.SetTagSortOrders(orderedIds, _domain);
        RefreshTags(sourceTag.Id);
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
