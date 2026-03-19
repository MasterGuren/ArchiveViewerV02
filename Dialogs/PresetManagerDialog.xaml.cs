using System.Windows;
using System.Windows.Input;
using ArchiveViewer.Models;

namespace ArchiveViewer.Dialogs;

public class PresetManagerResult
{
    public Dictionary<string, PresetData> Presets { get; set; } = new();
    public string Selected { get; set; } = "";
}

public partial class PresetManagerDialog : Window
{
    private readonly Dictionary<string, PresetData> _presets;
    private List<string> _order;
    public PresetManagerResult? Result { get; private set; }

    public PresetManagerDialog(Dictionary<string, PresetData> presets, string currentPreset)
    {
        InitializeComponent();
        _presets = new Dictionary<string, PresetData>(presets);
        _order = [.. _presets.Keys];
        RefreshList();

        var idx = _order.IndexOf(currentPreset);
        if (idx >= 0) PresetList.SelectedIndex = idx;
    }

    private void RefreshList()
    {
        PresetList.Items.Clear();
        foreach (var name in _order)
            PresetList.Items.Add(name);
    }

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new InputDialog("新規プリセット", "プリセット名:");
        dlg.Owner = this;
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.InputText))
        {
            var name = dlg.InputText;
            if (!_presets.ContainsKey(name))
            {
                _presets[name] = new PresetData();
                _order.Add(name);
                RefreshList();
            }
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (PresetList.SelectedIndex < 0) return;
        var srcName = _order[PresetList.SelectedIndex];
        var dlg = new InputDialog("コピー", "新しい名前:");
        dlg.Owner = this;
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.InputText))
        {
            var name = dlg.InputText;
            if (!_presets.ContainsKey(name))
            {
                _presets[name] = _presets[srcName].Clone();
                _order.Add(name);
                RefreshList();
            }
        }
    }

    private void BtnRename_Click(object sender, RoutedEventArgs e)
    {
        if (PresetList.SelectedIndex < 0) return;
        var oldName = _order[PresetList.SelectedIndex];
        var dlg = new InputDialog("名前変更", "新しい名前:");
        dlg.Owner = this;
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.InputText))
        {
            var newName = dlg.InputText;
            if (newName != oldName && !_presets.ContainsKey(newName))
            {
                _presets[newName] = _presets[oldName];
                _presets.Remove(oldName);
                _order[PresetList.SelectedIndex] = newName;
                RefreshList();
            }
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (PresetList.SelectedIndex < 0) return;
        var name = _order[PresetList.SelectedIndex];
        if (MessageBox.Show($"「{name}」を削除しますか？", "確認", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            _presets.Remove(name);
            _order.RemoveAt(PresetList.SelectedIndex);
            RefreshList();
        }
    }

    private void BtnUp_Click(object sender, RoutedEventArgs e)
    {
        int idx = PresetList.SelectedIndex;
        if (idx > 0)
        {
            (_order[idx - 1], _order[idx]) = (_order[idx], _order[idx - 1]);
            RefreshList();
            PresetList.SelectedIndex = idx - 1;
        }
    }

    private void BtnDown_Click(object sender, RoutedEventArgs e)
    {
        int idx = PresetList.SelectedIndex;
        if (idx >= 0 && idx < _order.Count - 1)
        {
            (_order[idx + 1], _order[idx]) = (_order[idx], _order[idx + 1]);
            RefreshList();
            PresetList.SelectedIndex = idx + 1;
        }
    }

    private void BtnLoad_Click(object sender, RoutedEventArgs e) => LoadSelected();

    private void PresetList_DoubleClick(object sender, MouseButtonEventArgs e) => LoadSelected();

    private void LoadSelected()
    {
        if (PresetList.SelectedIndex < 0) return;
        var ordered = new Dictionary<string, PresetData>();
        foreach (var name in _order)
            ordered[name] = _presets[name];

        Result = new PresetManagerResult
        {
            Presets = ordered,
            Selected = _order[PresetList.SelectedIndex]
        };
        DialogResult = true;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        var ordered = new Dictionary<string, PresetData>();
        foreach (var name in _order)
            ordered[name] = _presets[name];

        Result = new PresetManagerResult { Presets = ordered };
        DialogResult = true;
    }
}
