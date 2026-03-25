using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArchiveViewer.Models;

namespace ArchiveViewer.Dialogs;

public class SettingsResult
{
    public List<ActionItem> Actions { get; set; } = [];
    public List<string> Folders { get; set; } = [];
    public string TrashFolder { get; set; } = "";
    public bool ActionsSaved { get; set; }
    public bool FoldersSaved { get; set; }
    public bool TrashSaved { get; set; }
}

public partial class SettingsDialog : Window
{
    private readonly List<ActionItem> _actions;
    private readonly List<string> _folders;
    private string _trashFolder;
    private readonly string _folderSectionTitle;
    private readonly bool _isVideo;

    public SettingsResult Result { get; } = new();

    // Track which sections were saved
    private bool _actionsSaved;
    private bool _foldersSaved;
    private bool _trashSaved;

    // UI containers
    private StackPanel _actionsPanel = null!;
    private StackPanel _foldersPanel = null!;
    private StackPanel _trashPanel = null!;

    public SettingsDialog(List<ActionItem> actions, List<string> folders, string trashFolder,
        string folderSectionTitle = "ソースフォルダ", bool isVideo = false)
    {
        InitializeComponent();
        _actions = actions.Select(a => a.Clone()).ToList();
        _folders = new List<string>(folders);
        _trashFolder = trashFolder;
        _folderSectionTitle = folderSectionTitle;
        _isVideo = isVideo;

        BuildUI();
    }

    private void BuildUI()
    {
        MainPanel.Children.Clear();

        // === Actions Section ===
        BuildSectionHeader("評価アクション", () => SaveActions());
        _actionsPanel = new StackPanel();
        MainPanel.Children.Add(_actionsPanel);
        RebuildActions();

        AddSeparator();

        // === Folders Section ===
        BuildSectionHeader(_folderSectionTitle, () => SaveFolders());
        _foldersPanel = new StackPanel();
        MainPanel.Children.Add(_foldersPanel);
        RebuildFolders();

        AddSeparator();

        // === Trash Section ===
        BuildSectionHeader("ゴミ箱フォルダ", () => SaveTrash());
        _trashPanel = new StackPanel();
        MainPanel.Children.Add(_trashPanel);
        RebuildTrash();
    }

    // ======== SECTION HEADER WITH SAVE BUTTON ========

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

    // ======== ACTIONS ========

    private void RebuildActions()
    {
        _actionsPanel.Children.Clear();

        for (int i = 0; i < _actions.Count; i++)
        {
            var action = _actions[i];
            var idx = i;
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });  // Color
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // Label
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Path
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });  // Browse
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });  // Copy toggle
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });  // Up
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });  // Down
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });  // Delete

            // Color button
            var colorBtn = new Button
            {
                Width = 28, Height = 28,
                Background = Brush(action.Color),
                BorderBrush = Theme.BorderBrush,
                BorderThickness = new Thickness(2),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "色を変更"
            };
            colorBtn.Click += (_, _) =>
            {
                var dlg = new ColorPickerDialog(action.Color);
                dlg.Owner = this;
                if (dlg.ShowDialog() == true)
                {
                    action.Color = dlg.SelectedColor;
                    RebuildActions();
                }
            };
            Grid.SetColumn(colorBtn, 0);
            row.Children.Add(colorBtn);

            // Label
            var labelBox = MakeTextBox(action.Label);
            labelBox.Margin = new Thickness(4, 0, 4, 0);
            labelBox.LostFocus += (_, _) => action.Label = labelBox.Text;
            Grid.SetColumn(labelBox, 1);
            row.Children.Add(labelBox);

            // Path
            var pathBox = MakeTextBox(action.Folder);
            pathBox.Margin = new Thickness(0, 0, 2, 0);
            pathBox.LostFocus += (_, _) => action.Folder = pathBox.Text;
            Grid.SetColumn(pathBox, 2);
            row.Children.Add(pathBox);

            // Browse button
            var browseBtn = MakeSmallButton("...");
            browseBtn.Click += (_, _) =>
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog();
                if (!string.IsNullOrEmpty(action.Folder) && Directory.Exists(action.Folder))
                    dlg.SelectedPath = action.Folder;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    action.Folder = dlg.SelectedPath;
                    pathBox.Text = dlg.SelectedPath;
                }
            };
            Grid.SetColumn(browseBtn, 3);
            row.Children.Add(browseBtn);

            // Copy toggle
            var copyBtn = MakeSmallButton(action.Copy ? "複製" : "移動");
            copyBtn.Click += (_, _) =>
            {
                action.Copy = !action.Copy;
                RebuildActions();
            };
            Grid.SetColumn(copyBtn, 4);
            row.Children.Add(copyBtn);

            // Up
            if (i > 0)
            {
                var upBtn = MakeSmallButton("▲");
                upBtn.Click += (_, _) =>
                {
                    (_actions[idx - 1], _actions[idx]) = (_actions[idx], _actions[idx - 1]);
                    RebuildActions();
                };
                Grid.SetColumn(upBtn, 5);
                row.Children.Add(upBtn);
            }

            // Down
            if (i < _actions.Count - 1)
            {
                var downBtn = MakeSmallButton("▼");
                downBtn.Click += (_, _) =>
                {
                    (_actions[idx + 1], _actions[idx]) = (_actions[idx], _actions[idx + 1]);
                    RebuildActions();
                };
                Grid.SetColumn(downBtn, 6);
                row.Children.Add(downBtn);
            }

            // Delete
            var delBtn = MakeSmallButton("×");
            delBtn.Foreground = Brush("#ef4444");
            delBtn.Click += (_, _) =>
            {
                _actions.RemoveAt(idx);
                RebuildActions();
            };
            Grid.SetColumn(delBtn, 7);
            row.Children.Add(delBtn);

            _actionsPanel.Children.Add(row);
        }

        // Add button
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
            Margin = new Thickness(0, 4, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        addBtn.Click += (_, _) =>
        {
            _actions.Add(new ActionItem { Label = "新規", Color = Theme.ColorChoices[0] });
            RebuildActions();
        };
        _actionsPanel.Children.Add(addBtn);
    }

    // ======== FOLDERS ========

    private void RebuildFolders()
    {
        _foldersPanel.Children.Clear();

        for (int i = 0; i < _folders.Count; i++)
        {
            var idx = i;
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Path
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });  // Browse
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });  // Up
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });  // Down
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });  // Delete

            // Path
            var pathBox = MakeTextBox(_folders[i]);
            pathBox.Margin = new Thickness(0, 0, 2, 0);
            pathBox.LostFocus += (_, _) => _folders[idx] = pathBox.Text;
            Grid.SetColumn(pathBox, 0);
            row.Children.Add(pathBox);

            // Browse
            var browseBtn = MakeSmallButton("...");
            browseBtn.Click += (_, _) =>
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog();
                if (!string.IsNullOrEmpty(_folders[idx]) && Directory.Exists(_folders[idx]))
                    dlg.SelectedPath = _folders[idx];
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _folders[idx] = dlg.SelectedPath;
                    pathBox.Text = dlg.SelectedPath;
                }
            };
            Grid.SetColumn(browseBtn, 1);
            row.Children.Add(browseBtn);

            // Up
            if (i > 0)
            {
                var upBtn = MakeSmallButton("▲");
                upBtn.Click += (_, _) =>
                {
                    (_folders[idx - 1], _folders[idx]) = (_folders[idx], _folders[idx - 1]);
                    RebuildFolders();
                };
                Grid.SetColumn(upBtn, 2);
                row.Children.Add(upBtn);
            }

            // Down
            if (i < _folders.Count - 1)
            {
                var downBtn = MakeSmallButton("▼");
                downBtn.Click += (_, _) =>
                {
                    (_folders[idx + 1], _folders[idx]) = (_folders[idx], _folders[idx + 1]);
                    RebuildFolders();
                };
                Grid.SetColumn(downBtn, 3);
                row.Children.Add(downBtn);
            }

            // Delete
            var delBtn = MakeSmallButton("×");
            delBtn.Foreground = Brush("#ef4444");
            delBtn.Click += (_, _) =>
            {
                _folders.RemoveAt(idx);
                RebuildFolders();
            };
            Grid.SetColumn(delBtn, 4);
            row.Children.Add(delBtn);

            _foldersPanel.Children.Add(row);
        }

        // Add button
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
            Margin = new Thickness(0, 4, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        addBtn.Click += (_, _) =>
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _folders.Add(dlg.SelectedPath);
                RebuildFolders();
            }
        };
        _foldersPanel.Children.Add(addBtn);
    }

    // ======== TRASH ========

    private void RebuildTrash()
    {
        _trashPanel.Children.Clear();

        var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });

        var pathBox = MakeTextBox(_trashFolder);
        pathBox.Margin = new Thickness(0, 0, 2, 0);
        pathBox.LostFocus += (_, _) => _trashFolder = pathBox.Text;
        Grid.SetColumn(pathBox, 0);
        row.Children.Add(pathBox);

        var browseBtn = MakeSmallButton("...");
        browseBtn.Click += (_, _) =>
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (!string.IsNullOrEmpty(_trashFolder) && Directory.Exists(_trashFolder))
                dlg.SelectedPath = _trashFolder;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _trashFolder = dlg.SelectedPath;
                pathBox.Text = dlg.SelectedPath;
            }
        };
        Grid.SetColumn(browseBtn, 1);
        row.Children.Add(browseBtn);

        // Clear
        var clearBtn = MakeSmallButton("×");
        clearBtn.Foreground = Brush("#ef4444");
        clearBtn.Click += (_, _) =>
        {
            _trashFolder = "";
            RebuildTrash();
        };
        Grid.SetColumn(clearBtn, 2);
        row.Children.Add(clearBtn);

        _trashPanel.Children.Add(row);
    }

    // ======== SAVE HANDLERS ========

    private void SaveActions()
    {
        Result.Actions = _actions.Select(a => a.Clone()).ToList();
        Result.ActionsSaved = true;
        _actionsSaved = true;
        MessageBox.Show("評価アクションを保存しました。", "保存", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveFolders()
    {
        Result.Folders = new List<string>(_folders);
        Result.FoldersSaved = true;
        _foldersSaved = true;
        MessageBox.Show($"{_folderSectionTitle}を保存しました。", "保存", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveTrash()
    {
        Result.TrashFolder = _trashFolder;
        Result.TrashSaved = true;
        _trashSaved = true;
        MessageBox.Show("ゴミ箱フォルダを保存しました。", "保存", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ======== CLOSE ========

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = _actionsSaved || _foldersSaved || _trashSaved;
    }

    // ======== UI HELPERS ========

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

    private void AddSeparator()
    {
        MainPanel.Children.Add(new Border
        {
            Height = 1,
            Background = Theme.BorderBrush,
            Margin = new Thickness(0, 12, 0, 4)
        });
    }
}
