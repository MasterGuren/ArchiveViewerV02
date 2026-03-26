using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArchiveViewer.Models;

namespace ArchiveViewer.Dialogs;

public partial class RatingSettingsDialog : Window
{
    private readonly RatingPresetData _data;
    private readonly int _judgmentLevel;
    private bool _saved;

    public RatingPresetData? Result { get; private set; }

    public RatingSettingsDialog(RatingPresetData data, int judgmentLevel)
    {
        InitializeComponent();
        _data = data;
        _judgmentLevel = judgmentLevel;
        BuildUI();
    }

    private void BuildUI()
    {
        MainPanel.Children.Clear();

        // === Rating Folders ===
        BuildSectionHeader("評価中フォルダ (★0〜★9)", () => Save());
        for (int i = 0; i <= 9; i++)
        {
            var idx = i;
            BuildFolderRow($"★{i}", _data.RatingFolders[i], (path) => _data.RatingFolders[idx] = path);
        }

        AddSeparator();

        // === Confirmed Folders ===
        BuildSectionHeader("確定フォルダ (★0確〜★9確)", () => Save());
        for (int i = 0; i <= 9; i++)
        {
            var idx = i;
            BuildFolderRow($"★{i}確", _data.ConfirmedFolders[i], (path) => _data.ConfirmedFolders[idx] = path);
        }

        AddSeparator();

        // === Delete Folders ===
        BuildSectionHeader("削除フォルダ (★0〜★9)", () => Save());
        for (int i = 0; i <= 9; i++)
        {
            var idx = i;
            BuildFolderRow($"★{i}削除", _data.DeleteFolders[i], (path) => _data.DeleteFolders[idx] = path);
        }

        AddSeparator();

        // === Category Move Folder (0→1 only) ===
        BuildSectionHeader("カテゴリー移動フォルダ (0→1判定のみ)", () => Save());
        AddHelpText("0→1判定時に表示される「▶▲カテゴリー移動」ボタンの移動先フォルダです。");
        BuildFolderRow("移動先", _data.CategoryMoveFolder, (path) => _data.CategoryMoveFolder = path);

        AddSeparator();

        // === Source Folders per Judgment Level ===
        BuildSectionHeader($"ソースフォルダ (判定レベル別)", () => Save());
        AddHelpText("各判定レベルで使うソースフォルダを設定。未設定の場合は対応する評価中フォルダが使われます。");

        for (int level = 0; level <= 8; level++)
        {
            var lv = level;
            AddLabel($"  {level}→{level + 1} 判定:");

            if (!_data.SourceFolders.ContainsKey(level))
                _data.SourceFolders[level] = [];

            var sources = _data.SourceFolders[level];
            var sourcesPanel = new StackPanel();
            BuildSourceFolderList(sourcesPanel, sources);
            MainPanel.Children.Add(sourcesPanel);
        }
    }

    private void BuildSourceFolderList(StackPanel panel, List<string> sources)
    {
        panel.Children.Clear();

        for (int i = 0; i < sources.Count; i++)
        {
            var idx = i;
            var row = new Grid { Margin = new Thickness(16, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });

            var pathBox = MakeTextBox(sources[i]);
            pathBox.LostFocus += (_, _) => sources[idx] = pathBox.Text;
            Grid.SetColumn(pathBox, 0);
            row.Children.Add(pathBox);

            var browseBtn = MakeSmallButton("...");
            browseBtn.Click += (_, _) =>
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog();
                if (!string.IsNullOrEmpty(sources[idx]) && Directory.Exists(sources[idx]))
                    dlg.SelectedPath = sources[idx];
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    sources[idx] = dlg.SelectedPath;
                    pathBox.Text = dlg.SelectedPath;
                }
            };
            Grid.SetColumn(browseBtn, 1);
            row.Children.Add(browseBtn);

            var delBtn = MakeSmallButton("×");
            delBtn.Foreground = Brush("#ef4444");
            delBtn.Click += (_, _) =>
            {
                sources.RemoveAt(idx);
                BuildSourceFolderList(panel, sources);
            };
            Grid.SetColumn(delBtn, 2);
            row.Children.Add(delBtn);

            panel.Children.Add(row);
        }

        var addBtn = new Button
        {
            Content = "+ 追加",
            Background = Theme.PanelBrush,
            Foreground = Theme.TextBrush,
            BorderBrush = Theme.BorderBrush,
            FontFamily = new FontFamily("Yu Gothic UI"),
            FontSize = 11,
            Padding = new Thickness(8, 3, 8, 3),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Margin = new Thickness(16, 2, 0, 2),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        addBtn.Click += (_, _) =>
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                sources.Add(dlg.SelectedPath);
                BuildSourceFolderList(panel, sources);
            }
        };
        panel.Children.Add(addBtn);
    }

    private void BuildFolderRow(string label, string currentPath, Action<string> onPathChanged)
    {
        var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });

        var labelBlock = new TextBlock
        {
            Text = label,
            Foreground = Theme.TextBrush,
            FontFamily = new FontFamily("Yu Gothic UI"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(labelBlock, 0);
        row.Children.Add(labelBlock);

        var pathBox = MakeTextBox(currentPath);
        pathBox.Margin = new Thickness(4, 0, 2, 0);
        pathBox.LostFocus += (_, _) => onPathChanged(pathBox.Text);
        Grid.SetColumn(pathBox, 1);
        row.Children.Add(pathBox);

        var browseBtn = MakeSmallButton("...");
        browseBtn.Click += (_, _) =>
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (!string.IsNullOrEmpty(pathBox.Text) && Directory.Exists(pathBox.Text))
                dlg.SelectedPath = pathBox.Text;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                pathBox.Text = dlg.SelectedPath;
                onPathChanged(dlg.SelectedPath);
            }
        };
        Grid.SetColumn(browseBtn, 2);
        row.Children.Add(browseBtn);

        MainPanel.Children.Add(row);
    }

    private void Save()
    {
        // Clean up empty source folder entries
        foreach (var kv in _data.SourceFolders)
            kv.Value.RemoveAll(string.IsNullOrWhiteSpace);

        Result = _data;
        _saved = true;
        MessageBox.Show("設定を保存しました。", "保存", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = _saved;
    }

    // ======== UI HELPERS ========

    private void BuildSectionHeader(string title, Action onSave)
    {
        var header = new DockPanel { Margin = new Thickness(0, 8, 0, 4) };

        var titleBlock = new TextBlock
        {
            Text = title,
            Foreground = Theme.TextBrush,
            FontFamily = new FontFamily("Yu Gothic UI"),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(titleBlock, Dock.Left);
        header.Children.Add(titleBlock);

        var saveBtn = new Button
        {
            Content = "💾 保存",
            Background = Brush("#22c55e"),
            Foreground = Theme.TextBrush,
            BorderBrush = Theme.BorderBrush,
            FontFamily = new FontFamily("Yu Gothic UI"),
            FontSize = 11,
            Padding = new Thickness(12, 3, 12, 3),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        saveBtn.Click += (_, _) => onSave();
        DockPanel.SetDock(saveBtn, Dock.Right);
        header.Children.Add(saveBtn);

        MainPanel.Children.Add(header);
    }

    private void AddLabel(string text)
    {
        MainPanel.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = Theme.SubtextBrush,
            FontFamily = new FontFamily("Yu Gothic UI"),
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 2)
        });
    }

    private void AddHelpText(string text)
    {
        MainPanel.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = Theme.SubtextBrush,
            FontFamily = new FontFamily("Yu Gothic UI"),
            FontSize = 11,
            FontStyle = FontStyles.Italic,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });
    }

    private void AddSeparator()
    {
        MainPanel.Children.Add(new Border
        {
            Height = 1,
            Background = Theme.BorderBrush,
            Margin = new Thickness(0, 12, 0, 4)
        });
    }

    private static TextBox MakeTextBox(string text) => new()
    {
        Text = text,
        Background = Theme.PanelBrush,
        Foreground = Theme.TextBrush,
        BorderBrush = Theme.BorderBrush,
        CaretBrush = Theme.TextBrush,
        FontFamily = new FontFamily("Yu Gothic UI"),
        FontSize = 11,
        Padding = new Thickness(4, 2, 4, 2),
        VerticalContentAlignment = VerticalAlignment.Center
    };

    private static Button MakeSmallButton(string text) => new()
    {
        Content = text,
        Background = Theme.PanelBrush,
        Foreground = Theme.TextBrush,
        BorderBrush = Theme.BorderBrush,
        FontFamily = new FontFamily("Yu Gothic UI"),
        FontSize = 11,
        Padding = new Thickness(2),
        Margin = new Thickness(1, 0, 1, 0),
        Cursor = System.Windows.Input.Cursors.Hand,
        MinWidth = 0
    };

    private static SolidColorBrush Brush(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex)!);
}
