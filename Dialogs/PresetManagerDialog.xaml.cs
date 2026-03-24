using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ArchiveViewer.Models;

namespace ArchiveViewer.Dialogs;

public class PresetManagerResult
{
    public Dictionary<string, PresetData> Presets { get; set; } = new();
    public string CurrentPreset { get; set; } = "";
}

public partial class PresetManagerDialog : Window
{
    private readonly Dictionary<string, PresetData> _presets;
    private string _currentPreset;
    private List<string> _order;
    private List<string> _categories = [];
    public PresetManagerResult? Result { get; private set; }

    public PresetManagerDialog(Dictionary<string, PresetData> presets, string currentPreset)
    {
        InitializeComponent();
        _presets = presets.ToDictionary(p => p.Key, p => p.Value.Clone());
        _currentPreset = currentPreset;
        _order = [.. _presets.Keys];

        // Collect existing categories
        _categories = _presets.Values
            .Select(p => p.Category)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        RefreshTree(currentPreset);
    }

    private void RefreshTree(string? selectPreset = null)
    {
        PresetTree.Items.Clear();

        // Group presets by category
        var uncategorized = _order.Where(n => string.IsNullOrEmpty(_presets[n].Category)).ToList();
        var byCategory = _order
            .Where(n => !string.IsNullOrEmpty(_presets[n].Category))
            .GroupBy(n => _presets[n].Category)
            .ToDictionary(g => g.Key, g => g.ToList());

        TreeViewItem? selectItem = null;

        // Uncategorized presets first
        foreach (var name in uncategorized)
        {
            var item = new TreeViewItem { Header = name, Tag = name };
            PresetTree.Items.Add(item);
            if (name == selectPreset) selectItem = item;
        }

        // Categorized presets
        foreach (var cat in _categories)
        {
            var catItem = new TreeViewItem
            {
                Header = $"📁 {cat}",
                Tag = $"__cat__{cat}",
                IsExpanded = true
            };

            if (byCategory.TryGetValue(cat, out var names))
            {
                foreach (var name in names)
                {
                    var item = new TreeViewItem { Header = name, Tag = name };
                    catItem.Items.Add(item);
                    if (name == selectPreset) selectItem = item;
                }
            }

            PresetTree.Items.Add(catItem);
        }

        if (selectItem != null)
        {
            selectItem.IsSelected = true;
            selectItem.BringIntoView();
        }
    }

    private string? GetSelectedPresetName()
    {
        if (PresetTree.SelectedItem is TreeViewItem item && item.Tag is string tag && !tag.StartsWith("__cat__"))
            return tag;
        return null;
    }

    private string? GetSelectedCategoryName()
    {
        if (PresetTree.SelectedItem is TreeViewItem item && item.Tag is string tag && tag.StartsWith("__cat__"))
            return tag["__cat__".Length..];
        return null;
    }

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new InputDialog("新規プリセット", "プリセット名:");
        dlg.Owner = this;
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.InputText))
        {
            var name = dlg.InputText;
            if (_presets.ContainsKey(name))
            {
                MessageBox.Show($"「{name}」は既に存在します。", "新規プリセット");
                return;
            }
            var preset = new PresetData();
            var cat = GetSelectedCategoryName();
            if (cat != null) preset.Category = cat;
            _presets[name] = preset;
            _order.Add(name);
            RefreshTree(name);
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        var srcName = GetSelectedPresetName();
        if (srcName == null) return;
        var dlg = new InputDialog("コピー", "新しい名前:", srcName);
        dlg.Owner = this;
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.InputText))
        {
            var name = dlg.InputText;
            if (_presets.ContainsKey(name))
            {
                MessageBox.Show($"「{name}」は既に存在します。", "コピー");
                return;
            }
            _presets[name] = _presets[srcName].Clone();
            _order.Add(name);
            RefreshTree(name);
        }
    }

    private void BtnRename_Click(object sender, RoutedEventArgs e)
    {
        var oldName = GetSelectedPresetName();
        if (oldName == null) return;
        var dlg = new InputDialog("名前変更", "新しい名前:", oldName);
        dlg.Owner = this;
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.InputText))
        {
            var newName = dlg.InputText;
            if (newName == oldName) return;
            if (_presets.ContainsKey(newName))
            {
                MessageBox.Show($"「{newName}」は既に存在します。", "名前変更");
                return;
            }
            _presets[newName] = _presets[oldName];
            _presets.Remove(oldName);
            var idx = _order.IndexOf(oldName);
            if (idx >= 0) _order[idx] = newName;
            if (_currentPreset == oldName) _currentPreset = newName;
            RefreshTree(newName);
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        var name = GetSelectedPresetName();
        if (name == null) return;
        if (MessageBox.Show($"「{name}」を削除しますか？", "確認", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            _presets.Remove(name);
            _order.Remove(name);
            if (_currentPreset == name)
                _currentPreset = _order.FirstOrDefault() ?? "";
            RefreshTree(_currentPreset);
        }
    }

    private void BtnUp_Click(object sender, RoutedEventArgs e)
    {
        var name = GetSelectedPresetName();
        if (name == null) return;
        int idx = _order.IndexOf(name);
        if (idx > 0)
        {
            (_order[idx - 1], _order[idx]) = (_order[idx], _order[idx - 1]);
            RefreshTree(name);
        }
    }

    private void BtnDown_Click(object sender, RoutedEventArgs e)
    {
        var name = GetSelectedPresetName();
        if (name == null) return;
        int idx = _order.IndexOf(name);
        if (idx >= 0 && idx < _order.Count - 1)
        {
            (_order[idx + 1], _order[idx]) = (_order[idx], _order[idx + 1]);
            RefreshTree(name);
        }
    }

    private void BtnChangeCategory_Click(object sender, RoutedEventArgs e)
    {
        var name = GetSelectedPresetName();
        if (name == null) return;

        if (_categories.Count == 0)
        {
            MessageBox.Show("先にカテゴリを追加してください。", "カテゴリ変更");
            return;
        }

        var current = _presets[name].Category;
        var dlg = new CategorySelectDialog(_categories, current);
        dlg.Owner = this;
        if (dlg.ShowDialog() == true)
        {
            _presets[name].Category = dlg.SelectedCategory;
            RefreshTree(name);
        }
    }

    private void BtnAddCategory_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new InputDialog("カテゴリ追加", "カテゴリ名:");
        dlg.Owner = this;
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.InputText))
        {
            var cat = dlg.InputText.Trim();
            if (!_categories.Contains(cat))
            {
                _categories.Add(cat);
                _categories.Sort();
                RefreshTree();
            }
        }
    }

    private void BtnRenameCategory_Click(object sender, RoutedEventArgs e)
    {
        var cat = GetSelectedCategoryName();
        if (cat == null) return;
        var dlg = new InputDialog("カテゴリ名変更", "新しいカテゴリ名:", cat);
        dlg.Owner = this;
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.InputText))
        {
            var newCat = dlg.InputText.Trim();
            if (newCat != cat)
            {
                foreach (var p in _presets.Values.Where(p => p.Category == cat))
                    p.Category = newCat;
                _categories[_categories.IndexOf(cat)] = newCat;
                _categories.Sort();
                RefreshTree();
            }
        }
    }

    private void BtnDeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        var cat = GetSelectedCategoryName();
        if (cat == null) return;
        if (MessageBox.Show($"カテゴリ「{cat}」を削除しますか？\n（プリセットは未分類に移動されます）", "確認", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            foreach (var p in _presets.Values.Where(p => p.Category == cat))
                p.Category = "";
            _categories.Remove(cat);
            RefreshTree();
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        var ordered = new Dictionary<string, PresetData>();
        foreach (var name in _order)
            ordered[name] = _presets[name];

        // リネーム・削除を追跡済みの_currentPresetを使用
        var current = ordered.ContainsKey(_currentPreset)
            ? _currentPreset
            : (ordered.Keys.FirstOrDefault() ?? "");

        Result = new PresetManagerResult { Presets = ordered, CurrentPreset = current };
        DialogResult = true;
    }
}
