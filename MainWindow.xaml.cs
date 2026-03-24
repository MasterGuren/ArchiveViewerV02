using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ArchiveViewer.Controls;
using ArchiveViewer.Dialogs;
using ArchiveViewer.Models;
using ArchiveViewer.Services;
using LibVLCSharp.Shared;
using Microsoft.Win32;

namespace ArchiveViewer;

public partial class MainWindow : Window
{
    // --- State ---
    private AppConfig _config = null!;
    private string _mode = "browse";

    // Archive state
    private string? _archivePath;
    private List<string> _imageNames = [];
    private byte[]?[]? _thumbData;
    private BitmapSource?[]? _thumbnails;
    private List<ImageCard> _cards = [];

    // Selection
    private int? _selectStart;
    private int? _selectEnd;
    private bool _selectionConfirmed;

    // Display
    private int _thumbSize = Theme.ThumbSizeDefault;
    private string _cardOrient = "portrait";
    private string _folderSort = "name";

    // Navigation
    private List<string> _folderArchives = [];
    private int _currentArchiveIndex = -1;

    // Viewer
    private int _viewerIndex;
    private bool _viewerOpen;

    // Extract
    private List<ExtractEntry> _extractEntries = [];
    private string _extractOutputFolder = "";

    // Video
    private string? _videoPath;
    private List<string> _videoFiles = [];
    private int _currentVideoIndex = -1;
    private LibVLC? _libVlc;
    private LibVLCSharp.Shared.MediaPlayer? _vlcPlayer;
    private DispatcherTimer? _videoTimer;
    private bool _seekDragging;
    private int _videoVolume = 80;
    private LibVLCSharp.WPF.VideoView? _videoView; // created/destroyed dynamically

    // Presets (3 mode sets)
    private Dictionary<string, PresetData> _presets = new();
    private string _currentPreset = "";
    private List<ActionItem> _actions = [];
    private List<string> _sourceFolders = [];
    private string _trashFolder = "";

    private Dictionary<string, PresetData> _extractPresets = new();
    private string _extractCurrentPreset = "";
    private List<ActionItem> _extractActions = [];
    private List<string> _extractSourceFolders = [];
    private string _extractTrashFolder = "";

    private Dictionary<string, PresetData> _videoPresets = new();
    private string _videoCurrentPreset = "";
    private List<ActionItem> _videoActions = [];
    private List<string> _videoFolders = [];
    private string _videoTrashFolder = "";

    // Loading
    private CancellationTokenSource? _loadCts;

    [DllImport("shell32.dll")]
    private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appId);


    public MainWindow()
    {
        InitializeComponent();
    }

    // ======== INITIALIZATION ========

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try { SetCurrentProcessExplicitAppUserModelID("ArchiveViewer.V02"); } catch { }

        _config = ConfigService.Load();
        LoadConfigToState();
        SwitchMode(_config.State.LastMode);
        UpdateSortButtons();
        UpdateOrientButtons();

    }

    private void LoadConfigToState()
    {
        _presets = _config.Presets;
        _currentPreset = _config.State.CurrentPreset;
        _extractPresets = _config.ExtractPresets;
        _extractCurrentPreset = _config.State.ExtractCurrentPreset;
        _extractOutputFolder = _config.State.ExtractOutputFolder;
        _videoPresets = _config.VideoPresets;
        _videoCurrentPreset = _config.State.VideoCurrentPreset;
        _folderSort = _config.State.FolderSort;
        _cardOrient = _config.State.CardOrient;
        _videoVolume = _config.State.VideoVolume;

        LoadPreset(_currentPreset, _presets, ref _actions, ref _sourceFolders, ref _trashFolder);
        LoadPreset(_extractCurrentPreset, _extractPresets, ref _extractActions, ref _extractSourceFolders, ref _extractTrashFolder);
        LoadPreset(_videoCurrentPreset, _videoPresets, ref _videoActions, ref _videoFolders, ref _videoTrashFolder, isVideo: true);
    }

    private void LoadPreset(string name, Dictionary<string, PresetData> presets,
        ref List<ActionItem> actions, ref List<string> folders, ref string trash,
        bool isVideo = false)
    {
        if (presets.TryGetValue(name, out var preset))
        {
            actions = preset.Actions.Select(a => a.Clone()).ToList();
            // Video presets store folders in VideoFolders, others in SourceFolders
            var src = isVideo ? preset.VideoFolders : preset.SourceFolders;
            // Fallback: if the expected list is empty, try the other
            if (src.Count == 0)
                src = isVideo ? preset.SourceFolders : preset.VideoFolders;
            folders = new List<string>(src);
            trash = preset.TrashFolder;
        }
        else
        {
            actions = ConfigService.GetDefaultActions();
            folders = [];
            trash = "";
        }
    }

    /// <summary>
    /// 状態をconfigオブジェクトに反映（メモリのみ、ファイルには書かない）
    /// </summary>
    private void SyncStateToConfig()
    {
        _config.State.CurrentPreset = _currentPreset;
        _config.State.ExtractCurrentPreset = _extractCurrentPreset;
        _config.State.VideoCurrentPreset = _videoCurrentPreset;
        _config.State.ExtractOutputFolder = _extractOutputFolder;
        _config.State.FolderSort = _folderSort;
        _config.State.CardOrient = _cardOrient;
        _config.State.VideoVolume = _videoVolume;
        _config.State.LastMode = _mode;
        _config.State.PresetOrder = [.. _presets.Keys];
        _config.State.ExtractPresetOrder = [.. _extractPresets.Keys];
        _config.State.VideoPresetOrder = [.. _videoPresets.Keys];
    }

    /// <summary>
    /// 状態のみファイルに保存（プリセット順序・選択・UI設定）
    /// </summary>
    private void SaveStateOnly()
    {
        SyncStateToConfig();
        ConfigService.SaveState(_config);
    }

    /// <summary>
    /// プリセットデータをファイルに保存（「保存」ボタン押下時のみ）
    /// </summary>
    private void SavePresets()
    {
        // 現在のプリセットデータをconfigに反映（既存のCategoryを保持、存在するプリセットのみ）
        if (!string.IsNullOrEmpty(_currentPreset) && _presets.ContainsKey(_currentPreset))
        {
            var existing = _presets[_currentPreset];
            _presets[_currentPreset] = new PresetData
            {
                Actions = _actions.Select(a => a.Clone()).ToList(),
                SourceFolders = new List<string>(_sourceFolders),
                TrashFolder = _trashFolder,
                Category = existing.Category
            };
        }
        _config.Presets = _presets;

        if (!string.IsNullOrEmpty(_extractCurrentPreset) && _extractPresets.ContainsKey(_extractCurrentPreset))
        {
            var existing = _extractPresets[_extractCurrentPreset];
            _extractPresets[_extractCurrentPreset] = new PresetData
            {
                Actions = _extractActions.Select(a => a.Clone()).ToList(),
                SourceFolders = new List<string>(_extractSourceFolders),
                TrashFolder = _extractTrashFolder,
                Category = existing.Category
            };
        }
        _config.ExtractPresets = _extractPresets;

        if (!string.IsNullOrEmpty(_videoCurrentPreset) && _videoPresets.ContainsKey(_videoCurrentPreset))
        {
            var existing = _videoPresets[_videoCurrentPreset];
            _videoPresets[_videoCurrentPreset] = new PresetData
            {
                Actions = _videoActions.Select(a => a.Clone()).ToList(),
                VideoFolders = new List<string>(_videoFolders),
                TrashFolder = _videoTrashFolder,
                Category = existing.Category
            };
        }
        _config.VideoPresets = _videoPresets;

        SyncStateToConfig();
        ConfigService.SavePresets(_config);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // モードとソート順を保持
        SaveStateOnly();
        StopVideo();
        _loadCts?.Cancel();
    }

    // ======== MODE SWITCHING ========

    private void SwitchMode(string mode)
    {
        _mode = mode;
        BtnBrowse.IsChecked = mode == "browse";
        BtnExtract.IsChecked = mode == "extract";
        BtnVideo.IsChecked = mode == "video";

        // Show/hide extract UI
        bool isExtract = mode == "extract";
        BtnExtractRange.Visibility = isExtract ? Visibility.Visible : Visibility.Collapsed;
        BtnClearSelection.Visibility = isExtract ? Visibility.Visible : Visibility.Collapsed;
        RightSidebarCol.Width = isExtract ? new GridLength(300) : new GridLength(0);
        RightSidebar.Visibility = isExtract ? Visibility.Visible : Visibility.Collapsed;

        // Show/hide video — モード切替時に動画を閉じる
        bool isVideo = mode == "video";
        if (!isVideo && _videoPath != null)
        {
            StopVideo();
            _videoPath = null;
            VideoOverlay.Visibility = Visibility.Collapsed;
        }
        else
        {
            VideoOverlay.Visibility = isVideo && _videoPath != null ? Visibility.Visible : Visibility.Collapsed;
        }

        // Show grid for browse/extract
        GridScroller.Visibility = isVideo ? Visibility.Collapsed : Visibility.Visible;

        if (isVideo)
        {
            EmptyMessage.Visibility = _videoPath != null ? Visibility.Collapsed : Visibility.Visible;
            if (EmptyMessage.Visibility == Visibility.Visible)
            {
                var tb = EmptyMessage.Children.OfType<TextBlock>().LastOrDefault();
                if (tb != null) tb.Text = "動画ファイルを「開く」で選択";
            }
        }
        else
        {
            EmptyMessage.Visibility = _archivePath != null ? Visibility.Collapsed : Visibility.Visible;
        }

        RebuildSidebar();
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e) => SwitchMode("browse");
    private void BtnExtract_Click(object sender, RoutedEventArgs e) => SwitchMode("extract");
    private void BtnVideo_Click(object sender, RoutedEventArgs e) => SwitchMode("video");

    // ======== SIDEBAR ========

    private void RebuildSidebar()
    {
        LeftSidebar.Children.Clear();

        switch (_mode)
        {
            case "browse":
                BuildBrowseSidebar();
                break;
            case "extract":
                BuildExtractSidebar();
                break;
            case "video":
                BuildVideoSidebar();
                break;
        }
    }

    private void BuildBrowseSidebar()
    {
        // Working folder
        if (_archivePath != null)
        {
            AddSidebarLabel("作業フォルダ");
            AddSidebarText(Path.GetDirectoryName(_archivePath) ?? "");
            AddSidebarSeparator();
        }

        // Preset
        BuildPresetSection(_currentPreset, _presets,
            (name) => { _currentPreset = name; LoadPreset(name, _presets, ref _actions, ref _sourceFolders, ref _trashFolder); SaveStateOnly(); RebuildSidebar(); },
            () => ManagePresets(ref _currentPreset, ref _presets, ref _actions, ref _sourceFolders, ref _trashFolder));

        // Settings button
        var settingsBtn = CreateSidebarButton("⚙ プリセット設定", () => OpenSettings(
            _actions, _sourceFolders, _trashFolder, "ソースフォルダ", false,
            (r) => { _actions = r.Actions; _sourceFolders = r.Folders; _trashFolder = r.TrashFolder; }));
        settingsBtn.Margin = new Thickness(0, 4, 0, 4);
        LeftSidebar.Children.Add(settingsBtn);

        AddSidebarSeparator();

        // Actions (execute only)
        BuildActionButtons(_actions, (a) => ExecuteAction(a));

        AddSidebarSeparator();

        // Source folders (open only)
        BuildFolderButtons("ソースフォルダ", _sourceFolders,
            (f) => OpenFromFolder(f),
            (f) => OpenRandomFromFolder(f));

        AddSidebarSeparator();
        BuildTrashDisplay(_trashFolder);
    }

    private void BuildExtractSidebar()
    {
        // Extract output folder
        AddSidebarLabel("抽出先フォルダ");
        var folderPanel = new StackPanel { Orientation = Orientation.Horizontal };
        var folderText = new TextBlock
        {
            Text = string.IsNullOrEmpty(_extractOutputFolder) ? "(未設定)" : _extractOutputFolder,
            Foreground = Theme.SubtextBrush,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 200
        };
        folderPanel.Children.Add(folderText);
        var folderBtn = CreateSidebarButton("📁", () =>
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _extractOutputFolder = dlg.SelectedPath;
                RebuildSidebar();
            }
        });
        folderPanel.Children.Add(folderBtn);
        LeftSidebar.Children.Add(folderPanel);

        AddSidebarSeparator();

        // Preset
        BuildPresetSection(_extractCurrentPreset, _extractPresets,
            (name) => { _extractCurrentPreset = name; LoadPreset(name, _extractPresets, ref _extractActions, ref _extractSourceFolders, ref _extractTrashFolder); SaveStateOnly(); RebuildSidebar(); },
            () => ManagePresets(ref _extractCurrentPreset, ref _extractPresets, ref _extractActions, ref _extractSourceFolders, ref _extractTrashFolder));

        // Settings button
        var settingsBtn = CreateSidebarButton("⚙ プリセット設定", () => OpenSettings(
            _extractActions, _extractSourceFolders, _extractTrashFolder, "ソースフォルダ", false,
            (r) => { _extractActions = r.Actions; _extractSourceFolders = r.Folders; _extractTrashFolder = r.TrashFolder; }));
        settingsBtn.Margin = new Thickness(0, 4, 0, 4);
        LeftSidebar.Children.Add(settingsBtn);

        AddSidebarSeparator();

        // Actions (execute only)
        BuildActionButtons(_extractActions, (a) => ExecuteAction(a));

        AddSidebarSeparator();

        // Source folders (open only)
        BuildFolderButtons("ソースフォルダ", _extractSourceFolders,
            (f) => OpenFromFolder(f),
            (f) => OpenRandomFromFolder(f));

        AddSidebarSeparator();
        BuildTrashDisplay(_extractTrashFolder);
    }

    private void BuildVideoSidebar()
    {
        // Video file
        if (_videoPath != null)
        {
            AddSidebarLabel("動画ファイル");
            AddSidebarText(Path.GetFileName(_videoPath));
        }

        var openBtn = CreateSidebarButton("📁 動画を開く", () => OpenVideoFile());
        openBtn.Margin = new Thickness(0, 4, 0, 8);
        LeftSidebar.Children.Add(openBtn);

        AddSidebarSeparator();

        // Preset
        BuildPresetSection(_videoCurrentPreset, _videoPresets,
            (name) => { _videoCurrentPreset = name; LoadPreset(name, _videoPresets, ref _videoActions, ref _videoFolders, ref _videoTrashFolder, isVideo: true); SaveStateOnly(); RebuildSidebar(); },
            () => ManagePresets(ref _videoCurrentPreset, ref _videoPresets, ref _videoActions, ref _videoFolders, ref _videoTrashFolder, isVideo: true));

        AddSidebarSeparator();

        // Settings button
        var settingsBtn = CreateSidebarButton("⚙ プリセット設定", () => OpenSettings(
            _videoActions, _videoFolders, _videoTrashFolder, "動画フォルダ", true,
            (r) => { _videoActions = r.Actions; _videoFolders = r.Folders; _videoTrashFolder = r.TrashFolder; }));
        settingsBtn.Margin = new Thickness(0, 4, 0, 4);
        LeftSidebar.Children.Add(settingsBtn);

        AddSidebarSeparator();

        // Actions (execute only)
        BuildActionButtons(_videoActions, (a) => ExecuteVideoAction(a));

        AddSidebarSeparator();

        // Video folders (open only)
        BuildFolderButtons("動画フォルダ", _videoFolders,
            (f) => OpenVideoFromFolder(f),
            (f) => OpenRandomVideoFromFolder(f));

        AddSidebarSeparator();
        BuildTrashDisplay(_videoTrashFolder);
    }

    // Sidebar helpers
    private void AddSidebarLabel(string text)
    {
        LeftSidebar.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = Theme.TextBrush,
            FontFamily = new FontFamily(Theme.FontFamily),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 4, 0, 4)
        });
    }

    private void AddSidebarText(string text)
    {
        LeftSidebar.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = Theme.SubtextBrush,
            FontFamily = new FontFamily(Theme.FontFamily),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        });
    }

    private void AddSidebarSeparator()
    {
        LeftSidebar.Children.Add(new Border
        {
            Height = 1,
            Background = Theme.BorderBrush,
            Margin = new Thickness(0, 8, 0, 8)
        });
    }

    private Button CreateSidebarButton(string text, Action onClick, string? bgColor = null)
    {
        var btn = new Button
        {
            Content = text,
            FontFamily = new FontFamily("Segoe UI Emoji, Yu Gothic UI"),
            FontSize = 13,
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(2),
            Cursor = Cursors.Hand,
            Background = bgColor != null
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgColor)!)
                : Theme.PanelBrush,
            Foreground = Theme.TextBrush,
            BorderBrush = Theme.BorderBrush,
            BorderThickness = new Thickness(1)
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    // ======== PRESET SECTION ========

    private void BuildPresetSection(string presetName, Dictionary<string, PresetData> presets,
        Action<string> onLoad, Action onManage)
    {
        AddSidebarLabel("プリセット");

        // モードごとのタブフィルタ
        if (!_presetTabFilters.ContainsKey(_mode))
            _presetTabFilters[_mode] = null;

        var tabFilter = _presetTabFilters[_mode];

        // 初期表示時: 現在のプリセットのカテゴリにタブを合わせる
        if (tabFilter == null && !string.IsNullOrEmpty(presetName)
            && presets.TryGetValue(presetName, out var curPreset)
            && !string.IsNullOrEmpty(curPreset.Category))
        {
            tabFilter = curPreset.Category;
            _presetTabFilters[_mode] = tabFilter;
        }
        tabFilter ??= "";

        var categories = presets.Values
            .Select(p => p.Category)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        // Determine active tab from current preset's category
        string activeTab = "";
        if (!string.IsNullOrEmpty(presetName) && presets.TryGetValue(presetName, out var currentData))
            activeTab = currentData.Category ?? "";

        // Category tabs
        if (categories.Count > 0)
        {
            var tabPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) };

            foreach (var cat in categories)
            {
                var c = cat;
                var tab = CreateTabButton(c, tabFilter == c, () =>
                {
                    _presetTabFilters[_mode] = c;
                    RebuildSidebar();
                });
                tabPanel.Children.Add(tab);
            }

            // "すべて" tab at the end
            var allTab = CreateTabButton("すべて", string.IsNullOrEmpty(tabFilter), () =>
            {
                _presetTabFilters[_mode] = "";
                RebuildSidebar();
            });
            tabPanel.Children.Add(allTab);

            LeftSidebar.Children.Add(tabPanel);
        }

        // Preset list filtered by active tab
        var filteredPresets = string.IsNullOrEmpty(tabFilter)
            ? presets.Keys.ToList()
            : presets.Where(p => p.Value.Category == tabFilter).Select(p => p.Key).ToList();

        var listBox = new System.Windows.Controls.ListBox
        {
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2a2a3e")),
            Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#e2e8f0")),
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3f3f5e")),
            FontFamily = new System.Windows.Media.FontFamily("Yu Gothic UI"),
            FontSize = 14,
            MaxHeight = 120,
            Margin = new Thickness(0, 0, 0, 4)
        };

        foreach (var name in filteredPresets)
        {
            listBox.Items.Add(new ListBoxItem { Content = name, Tag = name });
            if (name == presetName)
                listBox.SelectedIndex = listBox.Items.Count - 1;
        }

        listBox.SelectionChanged += (_, _) =>
        {
            if (listBox.SelectedItem is ListBoxItem item && item.Tag is string name && name != presetName)
                onLoad(name);
        };

        if (listBox.SelectedIndex >= 0)
            listBox.Loaded += (_, _) => listBox.ScrollIntoView(listBox.SelectedItem);

        LeftSidebar.Children.Add(listBox);

        var manageBtn = CreateSidebarButton("カテゴリ・リスト管理", onManage);
        manageBtn.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
        manageBtn.Margin = new Thickness(0, 0, 0, 4);
        LeftSidebar.Children.Add(manageBtn);
    }

    private Dictionary<string, string?> _presetTabFilters = new(); // mode -> filter (null=未初期化, ""=すべて)

    private Button CreateTabButton(string label, bool isActive, Action onClick)
    {
        var btn = new Button
        {
            Content = label,
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 2, 2),
            FontFamily = new System.Windows.Media.FontFamily("Yu Gothic UI"),
            FontSize = 13,
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isActive ? "#7c3aed" : "#2a2a3e")),
            Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#e2e8f0")),
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3f3f5e")),
            Cursor = Cursors.Hand
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }


    private void ManagePresets(ref string currentPreset, ref Dictionary<string, PresetData> presets,
        ref List<ActionItem> actions, ref List<string> folders, ref string trash, bool isVideo = false)
    {
        var dlg = new PresetManagerDialog(presets, currentPreset);
        dlg.Owner = this;
        if (dlg.ShowDialog() == true && dlg.Result != null)
        {
            presets = dlg.Result.Presets;
            // カレントプリセットが変わった場合（リネーム・削除）に追従
            if (currentPreset != dlg.Result.CurrentPreset)
            {
                currentPreset = dlg.Result.CurrentPreset;
                LoadPreset(currentPreset, presets, ref actions, ref folders, ref trash, isVideo);
            }
            // プリセットデータ（カテゴリ変更含む）と順序を保存
            SavePresets();
            RebuildSidebar();
        }
    }

    // ======== SETTINGS DIALOG ========

    private void OpenSettings(List<ActionItem> actions, List<string> folders, string trashFolder,
        string folderTitle, bool isVideo, Action<SettingsResult> onApply)
    {
        var dlg = new SettingsDialog(actions, folders, trashFolder, folderTitle, isVideo);
        dlg.Owner = this;
        if (dlg.ShowDialog() == true)
        {
            var r = dlg.Result;
            var applied = new SettingsResult();

            if (r.ActionsSaved) applied.Actions = r.Actions;
            else applied.Actions = actions;

            if (r.FoldersSaved) applied.Folders = r.Folders;
            else applied.Folders = folders;

            if (r.TrashSaved) applied.TrashFolder = r.TrashFolder;
            else applied.TrashFolder = trashFolder;

            applied.ActionsSaved = r.ActionsSaved;
            applied.FoldersSaved = r.FoldersSaved;
            applied.TrashSaved = r.TrashSaved;

            onApply(applied);

            // Save only the sections that were saved in the dialog
            if (r.ActionsSaved || r.FoldersSaved || r.TrashSaved)
            {
                SavePresets();
            }
            RebuildSidebar();
        }
    }

    // ======== SIDEBAR ACTION BUTTONS (execute only) ========

    private void BuildActionButtons(List<ActionItem> actions, Action<ActionItem> onExecute)
    {
        AddSidebarLabel("評価アクション");

        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };

            var actionBtn = CreateSidebarButton(action.Label, () => onExecute(action), action.Color);
            actionBtn.MinWidth = 80;
            actionBtn.ToolTip = string.IsNullOrEmpty(action.Folder) ? "(フォルダ未設定)" : action.Folder;
            row.Children.Add(actionBtn);

            var modeText = new TextBlock
            {
                Text = action.Copy ? " 複製" : " 移動",
                Foreground = Theme.SubtextBrush,
                FontFamily = new FontFamily(Theme.FontFamily),
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0)
            };
            row.Children.Add(modeText);

            LeftSidebar.Children.Add(row);

            // Show folder path below the button
            if (!string.IsNullOrEmpty(action.Folder))
            {
                AddSidebarText($"  → {action.Folder}");
            }
        }
    }

    private void BuildTrashDisplay(string trashFolder)
    {
        AddSidebarLabel("ゴミ箱フォルダ");
        if (!string.IsNullOrEmpty(trashFolder))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock
            {
                Text = trashFolder,
                Foreground = Theme.SubtextBrush,
                FontFamily = new FontFamily(Theme.FontFamily),
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 200
            });
            row.Children.Add(CreateSidebarButton("📂", () =>
            {
                try { System.Diagnostics.Process.Start("explorer.exe", trashFolder.Replace('/', '\\')); } catch { }
            }));
            LeftSidebar.Children.Add(row);
        }
        else
        {
            AddSidebarText("(未設定)");
        }
    }

    // ======== SIDEBAR FOLDER BUTTONS (open only) ========

    private void BuildFolderButtons(string title, List<string> folders,
        Action<string> onOpen, Action<string> onRandom)
    {
        AddSidebarLabel(title);

        for (int i = 0; i < folders.Count; i++)
        {
            var folder = folders[i];
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };

            var folderBtn = CreateSidebarButton($"📂 {Path.GetFileName(folder)}", () => onOpen(folder));
            folderBtn.ToolTip = folder;
            row.Children.Add(folderBtn);

            row.Children.Add(CreateSidebarButton("🎲", () => onRandom(folder)));

            LeftSidebar.Children.Add(row);
        }
    }

    private void AddSourceFolder(List<string> folders)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog();
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            folders.Add(dlg.SelectedPath);
            RebuildSidebar();
        }
    }

    // ======== OPEN / LOAD ========

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        if (_mode == "video")
        {
            OpenVideoFile();
            return;
        }

        var dlg = new OpenFileDialog
        {
            Filter = "アーカイブ|*.zip;*.rar;*.7z;*.cbz;*.cbr|すべて|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            LoadArchive(dlg.FileName);
        }
    }

    private void OpenFromFolder(string folder)
    {
        var dlg = new OpenFileDialog
        {
            InitialDirectory = folder.Replace('/', '\\'),
            Filter = "アーカイブ|*.zip;*.rar;*.7z;*.cbz;*.cbr|すべて|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            LoadArchive(dlg.FileName);
        }
    }

    private void OpenRandomFromFolder(string folder)
    {
        try
        {
            var files = Directory.GetFiles(folder)
                .Where(f => Theme.ArchiveExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToArray();
            if (files.Length > 0)
            {
                var rnd = new Random();
                LoadArchive(files[rnd.Next(files.Length)]);
            }
        }
        catch (Exception ex) { SetStatus($"エラー: {ex.Message}"); }
    }

    private async void LoadArchive(string path, bool keepViewer = false)
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        _archivePath = path;
        _selectStart = null;
        _selectEnd = null;
        _selectionConfirmed = false;

        EmptyMessage.Visibility = Visibility.Collapsed;
        ProgressOverlay.Visibility = Visibility.Visible;
        if (!keepViewer)
        {
            ViewerOverlay.Visibility = Visibility.Collapsed;
            _viewerOpen = false;
        }
        ThumbnailGrid.Children.Clear();
        _cards.Clear();

        // Build folder file list
        BuildFolderArchives();
        UpdateNavigation();

        SetStatus($"読み込み中: {Path.GetFileName(path)}");

        try
        {
            await Task.Run(() =>
            {
                // Step 1: Read all raw image data from archive sequentially (thread-safe)
                using var archive = ArchiveFactory.Open(path);
                var names = archive.GetImageNames();
                if (ct.IsCancellationRequested) return;

                Dispatcher.Invoke(() =>
                {
                    _imageNames = names;
                    LoadProgress.Maximum = names.Count;
                });

                var rawImages = new byte[]?[names.Count];
                for (int i = 0; i < names.Count; i++)
                {
                    if (ct.IsCancellationRequested) return;
                    try { rawImages[i] = archive.ReadEntry(names[i]); }
                    catch { rawImages[i] = null; }
                }

                // Step 2: Generate thumbnails in parallel (no archive access needed)
                var data = new byte[]?[names.Count];
                int count = 0;
                var parallelOpts = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8),
                    CancellationToken = ct
                };

                Parallel.For(0, names.Count, parallelOpts, i =>
                {
                    if (rawImages[i] != null)
                        data[i] = ThumbnailService.GenerateThumbnailBytes(rawImages[i]!);

                    rawImages[i] = null; // Release original data immediately

                    var c = Interlocked.Increment(ref count);
                    if (c % 5 == 0 || c == names.Count)
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            LoadProgress.Value = c;
                            ProgressText.Text = $"読み込み中... {c}/{names.Count}";
                        });
                    }
                });

                if (ct.IsCancellationRequested) return;

                Dispatcher.Invoke(() =>
                {
                    _thumbData = data;
                    BuildThumbnails();
                    RebuildGrid();
                    ProgressOverlay.Visibility = Visibility.Collapsed;
                    SetStatus($"{Path.GetFileName(path)} — {names.Count}枚");
                    UpdateFileSize(path);
                    UpdateNavigation();

                    // If viewer was open, show first image of new archive
                    if (keepViewer && _viewerOpen && _imageNames.Count > 0)
                    {
                        _viewerIndex = 0;
                        ViewerSlider.Maximum = _imageNames.Count - 1;
                        _sliderUpdating = true;
                        ViewerSlider.Value = 0;
                        _sliderUpdating = false;
                        LoadViewerImage(0);
                    }
                });
            }, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ProgressOverlay.Visibility = Visibility.Collapsed;
            SetStatus($"エラー: {ex.Message}");
            ShowError($"アーカイブの読み込みに失敗しました:\n{ex.Message}");
        }
    }

    private void BuildThumbnails()
    {
        if (_thumbData == null) return;
        _thumbnails = new BitmapSource?[_thumbData.Length];
        for (int i = 0; i < _thumbData.Length; i++)
        {
            if (_thumbData[i] != null)
                _thumbnails[i] = ThumbnailService.CreateDisplayThumbnail(_thumbData[i]!, _thumbSize, _cardOrient);
        }
    }

    private void RebuildGrid()
    {
        ThumbnailGrid.Children.Clear();
        _cards.Clear();

        if (_thumbnails == null) return;

        int thumbW = _cardOrient == "portrait" ? _thumbSize * 2 / 3 : _thumbSize;
        int cardW = thumbW + 32;

        for (int i = 0; i < _imageNames.Count; i++)
        {
            var card = new ImageCard();
            var filename = Path.GetFileName(_imageNames[i]);
            card.Setup(i, filename, _thumbnails.Length > i ? _thumbnails[i] : null, _thumbSize);
            card.Width = cardW;
            card.CardClicked += OnCardClicked;
            card.CardDoubleClicked += OnCardDoubleClicked;
            _cards.Add(card);
            ThumbnailGrid.Children.Add(card);
        }

        UpdateSelectionVisuals();
        UpdateSelectionInfo();
        GridScroller.ScrollToTop();
    }

    // ======== SELECTION ========

    private void OnCardClicked(int index, MouseButtonEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && _selectStart.HasValue)
        {
            _selectEnd = index;
            _selectionConfirmed = true;
        }
        else
        {
            _selectStart = index;
            _selectEnd = null;
            _selectionConfirmed = false;
        }
        UpdateSelectionVisuals();
        UpdateSelectionInfo();
    }

    private void OnCardDoubleClicked(int index)
    {
        OpenViewer(index);
    }

    private void UpdateSelectionVisuals()
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            bool selected = false;
            bool isRange = false;
            if (_selectStart.HasValue)
            {
                if (_selectEnd.HasValue)
                {
                    int s = Math.Min(_selectStart.Value, _selectEnd.Value);
                    int e = Math.Max(_selectStart.Value, _selectEnd.Value);
                    selected = i >= s && i <= e;
                    isRange = true;
                }
                else
                {
                    selected = i == _selectStart.Value;
                }
            }
            _cards[i].SetSelected(selected, isRange);
        }
    }

    private void UpdateSelectionInfo()
    {
        if (_selectStart.HasValue && _selectEnd.HasValue)
        {
            int s = Math.Min(_selectStart.Value, _selectEnd.Value);
            int e = Math.Max(_selectStart.Value, _selectEnd.Value);
            int count = e - s + 1;
            TxtSelectionInfo.Text = $"選択: #{s + 1} 〜 #{e + 1} ({count}枚)";
        }
        else if (_selectStart.HasValue)
        {
            TxtSelectionInfo.Text = $"選択: #{_selectStart.Value + 1}";
        }
        else
        {
            TxtSelectionInfo.Text = "";
        }
    }

    private void BtnClearSelection_Click(object sender, RoutedEventArgs e)
    {
        _selectStart = null;
        _selectEnd = null;
        _selectionConfirmed = false;
        UpdateSelectionVisuals();
        UpdateSelectionInfo();
    }

    private void SelectAll()
    {
        if (_imageNames.Count == 0) return;
        _selectStart = 0;
        _selectEnd = _imageNames.Count - 1;
        _selectionConfirmed = true;
        UpdateSelectionVisuals();
        UpdateSelectionInfo();
    }

    // ======== IMAGE VIEWER ========

    private void OpenViewer(int index)
    {
        if (_thumbData == null || _archivePath == null) return;
        _viewerIndex = index;
        _viewerOpen = true;
        ViewerOverlay.Visibility = Visibility.Visible;
        LoadViewerImage(index);
        ViewerSlider.Maximum = _imageNames.Count - 1;
        ViewerSlider.Value = index;
    }

    private void LoadViewerImage(int index)
    {
        if (_archivePath == null) return;
        try
        {
            using var archive = ArchiveFactory.Open(_archivePath);
            var data = archive.ReadEntry(_imageNames[index]);
            var img = ThumbnailService.LoadFullImage(data);
            ViewerImage.Source = img;
            ViewerInfo.Text = $"{Path.GetFileName(_archivePath)} — {Path.GetFileName(_imageNames[index])} ({index + 1}/{_imageNames.Count})";
        }
        catch (Exception ex)
        {
            SetStatus($"画像読み込みエラー: {ex.Message}");
        }
    }

    private void CloseViewer()
    {
        _viewerOpen = false;
        ViewerOverlay.Visibility = Visibility.Collapsed;
    }

    private void ViewerOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            CloseViewer();
    }

    private DispatcherTimer? _viewerBarHideTimer;

    private void ViewerOverlay_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(ViewerOverlay);
        var height = ViewerOverlay.ActualHeight;

        if (height - pos.Y <= 80)
        {
            ShowViewerBar();
        }
        else
        {
            // Start hide timer if not hovering the bar itself
            if (!ViewerBottomBar.IsMouseOver)
                StartViewerBarHideTimer();
        }
    }

    private void ShowViewerBar()
    {
        _viewerBarHideTimer?.Stop();
        ViewerBottomBar.Opacity = 1;
    }

    private void StartViewerBarHideTimer()
    {
        _viewerBarHideTimer?.Stop();
        _viewerBarHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _viewerBarHideTimer.Tick += (_, _) =>
        {
            if (!ViewerBottomBar.IsMouseOver)
                ViewerBottomBar.Opacity = 0;
            _viewerBarHideTimer?.Stop();
        };
        _viewerBarHideTimer.Start();
    }

    private void ViewerBottomBar_MouseEnter(object sender, MouseEventArgs e)
    {
        ShowViewerBar();
    }

    private void ViewerBottomBar_MouseLeave(object sender, MouseEventArgs e)
    {
        StartViewerBarHideTimer();
    }

    private bool _sliderUpdating;
    private void ViewerSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_sliderUpdating || !_viewerOpen) return;
        int idx = (int)e.NewValue;
        if (idx != _viewerIndex && idx >= 0 && idx < _imageNames.Count)
        {
            _viewerIndex = idx;
            LoadViewerImage(idx);
        }
    }

    private void ViewerNavigate(int delta)
    {
        int newIdx = _viewerIndex + delta;
        if (newIdx >= 0 && newIdx < _imageNames.Count)
        {
            _viewerIndex = newIdx;
            _sliderUpdating = true;
            ViewerSlider.Value = newIdx;
            _sliderUpdating = false;
            LoadViewerImage(newIdx);
        }
    }

    // ======== FILE NAVIGATION ========

    private string? _lastFolderDir;
    private string? _lastFolderSortMode;

    private void BuildFolderArchives(bool forceReshuffle = false)
    {
        if (_archivePath == null) return;
        var dir = Path.GetDirectoryName(_archivePath);
        if (dir == null) return;

        // Skip rebuild if same folder, same sort mode, and not forced (preserves random order)
        if (!forceReshuffle && dir == _lastFolderDir && _folderSort == _lastFolderSortMode && _folderArchives.Count > 0)
        {
            _currentArchiveIndex = _folderArchives.FindIndex(f =>
                string.Equals(Path.GetFullPath(f), Path.GetFullPath(_archivePath!), StringComparison.OrdinalIgnoreCase));
            return;
        }

        try
        {
            var files = Directory.GetFiles(dir)
                .Where(f => Theme.ArchiveExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            switch (_folderSort)
            {
                case "name":
                    files.Sort(NaturalStringComparer.Instance);
                    break;
                case "date":
                    files = files.OrderByDescending(f => File.GetLastWriteTime(f)).ToList();
                    break;
                case "random":
                    var rnd = new Random();
                    files = files.OrderBy(_ => rnd.Next()).ToList();
                    break;
            }

            _folderArchives = files;
            _lastFolderDir = dir;
            _lastFolderSortMode = _folderSort;
            _currentArchiveIndex = _folderArchives.FindIndex(f =>
                string.Equals(Path.GetFullPath(f), Path.GetFullPath(_archivePath!), StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            _folderArchives = [];
            _currentArchiveIndex = -1;
        }
    }

    private void UpdateNavigation()
    {
        if (_folderArchives.Count == 0 || _currentArchiveIndex < 0)
        {
            TxtPrevFile.Text = "";
            TxtCurrentFile.Text = _archivePath != null ? Path.GetFileName(_archivePath) : "";
            TxtFilePosition.Text = "";
            TxtNextFile.Text = "";
            return;
        }

        TxtCurrentFile.Text = Path.GetFileName(_archivePath) ?? "";
        TxtFilePosition.Text = $"({_currentArchiveIndex + 1}/{_folderArchives.Count})";
        TxtPrevFile.Text = _currentArchiveIndex > 0 ? Path.GetFileName(_folderArchives[_currentArchiveIndex - 1]) : "";
        TxtNextFile.Text = _currentArchiveIndex < _folderArchives.Count - 1
            ? Path.GetFileName(_folderArchives[_currentArchiveIndex + 1]) : "";
    }

    private void NavigateFile(int delta)
    {
        if (_mode == "video")
        {
            NavigateVideo(delta);
            return;
        }

        if (_folderArchives.Count == 0 || _currentArchiveIndex < 0) return;
        int newIdx = _currentArchiveIndex + delta;
        if (newIdx >= 0 && newIdx < _folderArchives.Count)
        {
            LoadArchive(_folderArchives[newIdx], keepViewer: _viewerOpen);
        }
    }

    private void BtnPrev_Click(object sender, RoutedEventArgs e) => NavigateFile(-1);
    private void BtnNext_Click(object sender, RoutedEventArgs e) => NavigateFile(1);

    // ======== SORTING ========

    private void UpdateSortButtons()
    {
        BtnSortName.IsChecked = _folderSort == "name";
        BtnSortDate.IsChecked = _folderSort == "date";
        BtnSortRandom.IsChecked = _folderSort == "random";
    }

    private void BtnSortName_Click(object sender, RoutedEventArgs e)
    {
        _folderSort = "name";
        UpdateSortButtons();
        RebuildFileListForSort();
    }

    private void BtnSortDate_Click(object sender, RoutedEventArgs e)
    {
        _folderSort = "date";
        UpdateSortButtons();
        RebuildFileListForSort();
    }

    private void BtnSortRandom_Click(object sender, RoutedEventArgs e)
    {
        _folderSort = "random";
        UpdateSortButtons();
        RebuildFileListForSort(forceReshuffle: true);
    }

    private void RebuildFileListForSort(bool forceReshuffle = false)
    {
        if (_mode == "video")
        {
            if (_videoPath != null) { BuildVideoFileList(forceReshuffle); UpdateVideoNavigation(); }
        }
        else
        {
            if (_archivePath != null) { BuildFolderArchives(forceReshuffle); UpdateNavigation(); }
        }
    }

    // ======== CARD ORIENTATION ========

    private void UpdateOrientButtons()
    {
        BtnPortrait.IsChecked = _cardOrient == "portrait";
        BtnLandscape.IsChecked = _cardOrient == "landscape";
    }

    private void BtnPortrait_Click(object sender, RoutedEventArgs e)
    {
        _cardOrient = "portrait";
        UpdateOrientButtons();
        if (_thumbData != null) { BuildThumbnails(); RebuildGrid(); }
    }

    private void BtnLandscape_Click(object sender, RoutedEventArgs e)
    {
        _cardOrient = "landscape";
        UpdateOrientButtons();
        if (_thumbData != null) { BuildThumbnails(); RebuildGrid(); }
    }

    // ======== ZOOM ========

    private void ZoomThumbnails(int delta)
    {
        int newSize = _thumbSize + delta * Theme.ThumbSizeStep;
        newSize = Math.Clamp(newSize, Theme.ThumbSizeMin, Theme.ThumbSizeMax);
        if (newSize != _thumbSize)
        {
            _thumbSize = newSize;
            if (_thumbData != null)
            {
                BuildThumbnails();
                RebuildGrid();
            }
        }
    }

    // ======== ACTIONS ========

    private async void ExecuteAction(ActionItem action)
    {
        if (_archivePath == null || string.IsNullOrEmpty(action.Folder)) return;

        var src = _archivePath;
        var dst = Path.Combine(action.Folder, Path.GetFileName(src));

        try
        {
            Directory.CreateDirectory(action.Folder);

            if (File.Exists(dst))
            {
                var resolution = ResolveConflict(src, dst);
                if (resolution == "skip") return;
                if (resolution == "rename") dst = MakeUniquePath(dst);
            }

            var verb = action.Copy ? "コピー" : "移動";
            var msg = $"{verb}中: {Path.GetFileName(src)}...";
            if (action.Copy)
                await CopyFileWithProgress(src, dst, msg);
            else
                await MoveFileWithProgress(src, dst, msg);
            File.SetLastWriteTime(dst, DateTime.Now);

            SetStatus($"{verb}しました: {Path.GetFileName(dst)}");
            if (!action.Copy) AfterFileAction();
        }
        catch (Exception ex)
        {
            ShowError($"ファイル操作に失敗しました:\n{ex.Message}");
        }
    }

    private async void ExecuteVideoAction(ActionItem action)
    {
        if (_videoPath == null || string.IsNullOrEmpty(action.Folder)) return;

        var src = _videoPath;
        var dst = Path.Combine(action.Folder, Path.GetFileName(src));

        try
        {
            Directory.CreateDirectory(action.Folder);

            if (File.Exists(dst))
            {
                var resolution = ResolveConflict(src, dst);
                if (resolution == "skip") return;
                if (resolution == "rename") dst = MakeUniquePath(dst);
            }

            StopVideo();

            var verb = action.Copy ? "コピー" : "移動";
            var msg = $"{verb}中: {Path.GetFileName(src)}...";
            if (action.Copy)
                await CopyFileWithProgress(src, dst, msg);
            else
                await MoveFileWithProgress(src, dst, msg);
            File.SetLastWriteTime(dst, DateTime.Now);

            SetStatus($"{verb}しました: {Path.GetFileName(dst)}");
            if (!action.Copy) AfterVideoAction();
        }
        catch (Exception ex)
        {
            ShowError($"ファイル操作に失敗しました:\n{ex.Message}");
        }
    }

    /// <summary>
    /// ファイルをコピー（進捗付き）。500ms以上かかる場合はプログレスダイアログを表示。
    /// </summary>
    private async Task CopyFileWithProgress(string src, string dst, string message)
    {
        var srcInfo = new FileInfo(src);
        long totalBytes = srcInfo.Length;

        ProgressDialog? dlg = null;
        var showTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        showTimer.Tick += (_, _) =>
        {
            showTimer.Stop();
            dlg = new ProgressDialog(message) { Owner = this };
            dlg.Show();
        };
        showTimer.Start();

        try
        {
            await Task.Run(() =>
            {
                using var srcStream = new FileStream(src, FileMode.Open, FileAccess.Read);
                using var dstStream = new FileStream(dst, FileMode.Create, FileAccess.Write);
                var buffer = new byte[1024 * 1024]; // 1MB buffer
                long copied = 0;
                int read;
                while ((read = srcStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    dstStream.Write(buffer, 0, read);
                    copied += read;
                    if (totalBytes > 0)
                    {
                        var pct = (double)copied / totalBytes * 100;
                        var copiedMB = copied / (1024.0 * 1024.0);
                        var totalMB = totalBytes / (1024.0 * 1024.0);
                        Dispatcher.BeginInvoke(() =>
                            dlg?.UpdateProgress(pct, $"{copiedMB:F1} MB / {totalMB:F1} MB ({pct:F0}%)"));
                    }
                }
            });
        }
        finally
        {
            showTimer.Stop();
            dlg?.Close();
        }
    }

    /// <summary>
    /// ファイルを移動（同ドライブならrename、異ドライブならコピー+削除で進捗表示）。
    /// </summary>
    private async Task MoveFileWithProgress(string src, string dst, string message)
    {
        // 同じドライブならrenameで一瞬
        if (Path.GetPathRoot(Path.GetFullPath(src))?.Equals(
                Path.GetPathRoot(Path.GetFullPath(dst)), StringComparison.OrdinalIgnoreCase) == true)
        {
            await Task.Run(() =>
            {
                if (File.Exists(dst)) File.Delete(dst);
                File.Move(src, dst);
            });
        }
        else
        {
            // 異なるドライブ → コピー+削除
            if (File.Exists(dst)) File.Delete(dst);
            await CopyFileWithProgress(src, dst, message);
            File.Delete(src);
        }
    }

    private string ResolveConflict(string src, string dst)
    {
        var srcSize = new FileInfo(src).Length;
        var dstSize = new FileInfo(dst).Length;
        string sizeCompare = srcSize > dstSize ? "移動元が大きい" :
                             srcSize < dstSize ? "移動先が大きい" : "同じサイズ";

        var dlg = new ConflictDialog(Path.GetFileName(dst),
            FormatSize(srcSize), FormatSize(dstSize), sizeCompare);
        dlg.Owner = this;
        dlg.ShowDialog();
        return dlg.Result; // "overwrite", "rename", "skip"
    }

    private static string MakeUniquePath(string path)
    {
        var dir = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        int n = 1;
        string result;
        do
        {
            n++;
            result = Path.Combine(dir, $"{stem} ({n}){ext}");
        } while (File.Exists(result));
        return result;
    }

    private void AfterFileAction()
    {
        int oldIdx = _currentArchiveIndex;

        // Remove the moved file from the cached list
        if (oldIdx >= 0 && oldIdx < _folderArchives.Count)
            _folderArchives.RemoveAt(oldIdx);

        if (_folderArchives.Count == 0)
        {
            _archivePath = null;
            ThumbnailGrid.Children.Clear();
            _cards.Clear();
            EmptyMessage.Visibility = Visibility.Visible;
            UpdateNavigation();
            return;
        }

        int idx = Math.Clamp(oldIdx, 0, _folderArchives.Count - 1);
        _archivePath = _folderArchives[idx];
        _currentArchiveIndex = idx;
        LoadArchive(_folderArchives[idx]);
    }

    private void AfterVideoAction()
    {
        int oldIdx = _currentVideoIndex;

        // Remove the moved file from the cached list (don't rebuild from disk,
        // because _videoPath now points to the moved destination)
        if (oldIdx >= 0 && oldIdx < _videoFiles.Count)
            _videoFiles.RemoveAt(oldIdx);

        if (_videoFiles.Count == 0)
        {
            _videoPath = null;
            _lastVideoDirCache = null;
            VideoOverlay.Visibility = Visibility.Collapsed;
            EmptyMessage.Visibility = Visibility.Visible;
            return;
        }

        int idx = Math.Clamp(oldIdx, 0, _videoFiles.Count - 1);
        _videoPath = _videoFiles[idx];
        _currentVideoIndex = idx;
        PlayVideo(_videoFiles[idx]);
    }

    // ======== EXTRACT ========

    private void BtnExtractRange_Click(object sender, RoutedEventArgs e)
    {
        if (_selectStart == null || _archivePath == null) return;

        int s = _selectStart.Value;
        int end = _selectEnd ?? s;
        if (s > end) (s, end) = (end, s);

        var dlg = new NameInputDialog(Path.GetFileNameWithoutExtension(_archivePath));
        dlg.Owner = this;
        if (dlg.ShowDialog() != true) return;

        var saveDialog = new SaveFileDialog
        {
            Filter = "ZIP|*.zip",
            FileName = dlg.GeneratedFileName
        };
        if (saveDialog.ShowDialog() != true) return;

        try
        {
            ExtractRangeToZip(s, end, saveDialog.FileName);
            SetStatus($"抽出完了: {Path.GetFileName(saveDialog.FileName)}");
        }
        catch (Exception ex)
        {
            ShowError($"抽出に失敗しました:\n{ex.Message}");
        }
    }

    private void ExtractRangeToZip(int start, int end, string outputPath)
    {
        if (_archivePath == null) return;
        using var archive = ArchiveFactory.Open(_archivePath);
        using var zip = new ZipArchive(File.Create(outputPath), ZipArchiveMode.Create);
        for (int i = start; i <= end; i++)
        {
            var data = archive.ReadEntry(_imageNames[i]);
            var entryName = Path.GetFileName(_imageNames[i]);
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            entryStream.Write(data);
        }
    }

    // Extract mode - right sidebar
    private void BtnAddToExtractList_Click(object sender, RoutedEventArgs e)
    {
        if (_selectStart == null) return;
        int s = _selectStart.Value;
        int end = _selectEnd ?? s;
        if (s > end) (s, end) = (end, s);

        _extractEntries.Add(new ExtractEntry { Start = s, End = end });
        RebuildExtractList();
        BtnClearSelection_Click(sender, e);
    }

    private void BtnExecuteExtract_Click(object sender, RoutedEventArgs e)
    {
        if (_extractEntries.Count == 0 || _archivePath == null) return;
        if (string.IsNullOrEmpty(_extractOutputFolder))
        {
            MessageBox.Show("抽出先フォルダを設定してください", "エラー");
            return;
        }

        try
        {
            Directory.CreateDirectory(_extractOutputFolder);
            int count = 0;
            foreach (var entry in _extractEntries.Where(en => !en.Extracted))
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(entry.Author)) parts.Add(entry.Author);
                if (!string.IsNullOrEmpty(entry.Title)) parts.Add(entry.Title);
                if (!string.IsNullOrEmpty(entry.Episode)) parts.Add(entry.Episode);
                parts.Add(Path.GetFileNameWithoutExtension(_archivePath));

                var fileName = string.Join("_", parts) + ".zip";
                var outputPath = Path.Combine(_extractOutputFolder, fileName);

                // Unique filename
                if (File.Exists(outputPath))
                {
                    var stem = Path.GetFileNameWithoutExtension(outputPath);
                    int n = 2;
                    while (File.Exists(outputPath))
                    {
                        outputPath = Path.Combine(_extractOutputFolder, $"{stem}_{n}.zip");
                        n++;
                    }
                }

                ExtractRangeToZip(entry.Start, entry.End, outputPath);
                entry.Extracted = true;
                count++;
            }

            RebuildExtractList();
            SetStatus($"{count}件の抽出が完了しました");
        }
        catch (Exception ex)
        {
            ShowError($"抽出に失敗しました:\n{ex.Message}");
        }
    }

    private void RebuildExtractList()
    {
        ExtractListPanel.Children.Clear();
        for (int i = 0; i < _extractEntries.Count; i++)
        {
            var entry = _extractEntries[i];
            var idx = i;

            var border = new Border
            {
                Background = entry.Extracted ? new SolidColorBrush(Color.FromArgb(30, 34, 197, 94)) : Theme.PanelBrush,
                BorderBrush = Theme.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 4)
            };

            var panel = new StackPanel();

            // Header row
            var headerRow = new DockPanel();
            headerRow.Children.Add(new TextBlock
            {
                Text = $"#{entry.Start + 1}〜#{entry.End + 1} ({entry.End - entry.Start + 1}枚)",
                Foreground = Theme.TextBrush,
                FontSize = 13,
                FontWeight = FontWeights.Bold
            });
            var removeBtn = CreateSidebarButton("×", () =>
            {
                _extractEntries.RemoveAt(idx);
                RebuildExtractList();
            });
            DockPanel.SetDock(removeBtn, Dock.Right);
            headerRow.Children.Insert(0, removeBtn);
            panel.Children.Add(headerRow);

            // Metadata fields
            AddExtractField(panel, "著者", entry.Author, v => entry.Author = v);
            AddExtractField(panel, "作品", entry.Title, v => entry.Title = v);
            AddExtractField(panel, "話", entry.Episode, v => entry.Episode = v);

            // Preview
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(entry.Author)) parts.Add(entry.Author);
            if (!string.IsNullOrEmpty(entry.Title)) parts.Add(entry.Title);
            if (!string.IsNullOrEmpty(entry.Episode)) parts.Add(entry.Episode);
            if (_archivePath != null) parts.Add(Path.GetFileNameWithoutExtension(_archivePath));
            var preview = string.Join("_", parts) + ".zip";
            panel.Children.Add(new TextBlock
            {
                Text = $"→ {preview}",
                Foreground = Theme.SubtextBrush,
                FontSize = 9,
                Margin = new Thickness(0, 2, 0, 0)
            });

            border.Child = panel;
            ExtractListPanel.Children.Add(border);
        }
    }

    private void AddExtractField(StackPanel panel, string label, string value, Action<string> onChange)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
        row.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Theme.SubtextBrush,
            FontSize = 14,
            Width = 30,
            VerticalAlignment = VerticalAlignment.Center
        });
        var tb = new TextBox
        {
            Text = value,
            Width = 180,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1e1e2e")!),
            Foreground = Theme.TextBrush,
            BorderBrush = Theme.BorderBrush,
            CaretBrush = Theme.TextBrush,
            FontSize = 13,
            Padding = new Thickness(4, 2, 4, 2)
        };
        tb.TextChanged += (_, _) => onChange(tb.Text);
        row.Children.Add(tb);
        panel.Children.Add(row);
    }

    // ======== VIDEO ========

    private void OpenVideoFile()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "動画|*.mp4;*.mkv;*.avi;*.wmv;*.webm;*.mov;*.flv;*.m4v|すべて|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            PlayVideo(dlg.FileName);
        }
    }

    private void OpenVideoFromFolder(string folder)
    {
        var dlg = new OpenFileDialog
        {
            InitialDirectory = folder.Replace('/', '\\'),
            Filter = "動画|*.mp4;*.mkv;*.avi;*.wmv;*.webm;*.mov;*.flv;*.m4v|すべて|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            PlayVideo(dlg.FileName);
        }
    }

    private void OpenRandomVideoFromFolder(string folder)
    {
        try
        {
            var files = Directory.GetFiles(folder)
                .Where(f => Theme.VideoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToArray();
            if (files.Length > 0)
            {
                var rnd = new Random();
                PlayVideo(files[rnd.Next(files.Length)]);
            }
        }
        catch (Exception ex) { SetStatus($"エラー: {ex.Message}"); }
    }

    private void PlayVideo(string path)
    {
        StopVideo();
        _videoPath = path;
        EmptyMessage.Visibility = Visibility.Collapsed;
        VideoOverlay.Visibility = Visibility.Visible;
        GridScroller.Visibility = Visibility.Collapsed;

        BuildVideoFileList();
        UpdateVideoNavigation();
        RebuildSidebar();

        try
        {
            if (_libVlc == null)
            {
                Core.Initialize();
                _libVlc = new LibVLC("--no-xlib", "--quiet", "--no-video-title-show");
            }

            _vlcPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVlc);
            _vlcPlayer.Volume = _videoVolume;
            VolumeSlider.Value = _videoVolume;
            VolumeText.Text = $"{_videoVolume}%";

            // Create VideoView dynamically and add to visual tree
            CreateVideoView();
            _videoView!.MediaPlayer = _vlcPlayer;

            // Use file path directly (not Uri) to avoid encoding issues with Japanese characters
            using var media = new Media(_libVlc, path, FromType.FromPath);
            _vlcPlayer.Play(media);

            _videoTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _videoTimer.Tick += VideoUpdateLoop;
            _videoTimer.Start();

            SetStatus($"再生中: {Path.GetFileName(path)}");
            UpdateFileSize(path);
        }
        catch (Exception ex)
        {
            ShowError($"動画再生に失敗しました:\n{ex.Message}");
        }
    }

    private void StopVideo()
    {
        _videoTimer?.Stop();
        _videoTimer = null;

        if (_vlcPlayer != null)
        {
            try
            {
                _vlcPlayer.Stop();
                var media = _vlcPlayer.Media;
                _vlcPlayer.Media = null;
                media?.Dispose();
                if (_videoView != null)
                    _videoView.MediaPlayer = null;
                _vlcPlayer.Dispose();
            }
            catch { }
            _vlcPlayer = null;

            Thread.Sleep(100);
        }

        // VideoViewをビジュアルツリーから完全除去（WindowsFormsHostのフォーカス問題を根治）
        DestroyVideoView();
    }

    private void CreateVideoView()
    {
        if (_videoView != null) return;
        _videoView = new LibVLCSharp.WPF.VideoView { Background = System.Windows.Media.Brushes.Black };
        // Airspace overlay for mouse events
        var overlay = new Grid { Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromArgb(1, 0, 0, 0)) };
        overlay.MouseLeftButtonDown += VideoSurface_Click;
        overlay.MouseWheel += VideoSurface_MouseWheel;
        overlay.MouseUp += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.XButton1) { NavigateVideo(-1); e.Handled = true; }
            else if (e.ChangedButton == MouseButton.XButton2) { NavigateVideo(1); e.Handled = true; }
        };
        _videoView.Content = overlay;
        VideoViewHost.Children.Add(_videoView);
    }

    private void DestroyVideoView()
    {
        if (_videoView == null) return;
        VideoViewHost.Children.Remove(_videoView);
        _videoView.Dispose();
        _videoView = null;
    }

    private string? _lastVideoDirCache;
    private string? _lastVideoSortMode;

    private void BuildVideoFileList(bool forceReshuffle = false)
    {
        if (_videoPath == null) return;
        var dir = Path.GetDirectoryName(_videoPath);
        if (dir == null) return;

        // Skip rebuild if same folder, same sort mode, and not forced (preserves random order)
        if (!forceReshuffle && dir == _lastVideoDirCache && _folderSort == _lastVideoSortMode && _videoFiles.Count > 0)
        {
            _currentVideoIndex = _videoFiles.FindIndex(f =>
                string.Equals(Path.GetFullPath(f), Path.GetFullPath(_videoPath!), StringComparison.OrdinalIgnoreCase));
            return;
        }

        try
        {
            var files = Directory.GetFiles(dir)
                .Where(f => Theme.VideoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            switch (_folderSort)
            {
                case "name":
                    files.Sort(NaturalStringComparer.Instance);
                    break;
                case "date":
                    files = files.OrderByDescending(f => File.GetLastWriteTime(f)).ToList();
                    break;
                case "random":
                    var rnd = new Random();
                    files = files.OrderBy(_ => rnd.Next()).ToList();
                    break;
            }

            _videoFiles = files;
            _lastVideoDirCache = dir;
            _lastVideoSortMode = _folderSort;
            _currentVideoIndex = _videoFiles.FindIndex(f =>
                string.Equals(Path.GetFullPath(f), Path.GetFullPath(_videoPath!), StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            _videoFiles = [];
            _currentVideoIndex = -1;
        }
    }

    private void UpdateVideoNavigation()
    {
        TxtCurrentFile.Text = _videoPath != null ? Path.GetFileName(_videoPath) : "";
        TxtFilePosition.Text = _videoPath != null ? $"({_currentVideoIndex + 1}/{_videoFiles.Count})" : "";
        TxtPrevFile.Text = _currentVideoIndex > 0 ? Path.GetFileName(_videoFiles[_currentVideoIndex - 1]) : "";
        TxtNextFile.Text = _currentVideoIndex < _videoFiles.Count - 1 ? Path.GetFileName(_videoFiles[_currentVideoIndex + 1]) : "";
    }

    private void BtnVideoPrev_Click(object sender, RoutedEventArgs e) => NavigateVideo(-1);
    private void BtnVideoNext_Click(object sender, RoutedEventArgs e) => NavigateVideo(1);

    private void NavigateVideo(int delta)
    {
        if (_videoFiles.Count == 0 || _currentVideoIndex < 0) return;
        int newIdx = _currentVideoIndex + delta;
        if (newIdx >= 0 && newIdx < _videoFiles.Count)
        {
            PlayVideo(_videoFiles[newIdx]);
        }
    }

    private void VideoUpdateLoop(object? sender, EventArgs e)
    {
        if (_vlcPlayer == null) return;

        var state = _vlcPlayer.State;
        var length = _vlcPlayer.Length;
        var time = _vlcPlayer.Time;

        if (length > 0 && !_seekDragging)
        {
            DrawSeekBar(time, length);
        }

        VideoTimeText.Text = $"{FormatTime(time)} / {FormatTime(length)}";

        BtnPlayPause.Content = state == VLCState.Playing ? "⏸" : "▶";
    }

    private void DrawSeekBar(long time, long length)
    {
        SeekBarCanvas.Children.Clear();
        double w = SeekBarCanvas.ActualWidth;
        double h = SeekBarCanvas.ActualHeight;
        if (w <= 0) return;

        double ratio = length > 0 ? (double)time / length : 0;

        // Track background
        var trackBg = new System.Windows.Shapes.Rectangle
        {
            Width = w, Height = 4,
            Fill = Theme.BorderBrush,
            RadiusX = 2, RadiusY = 2
        };
        Canvas.SetLeft(trackBg, 0);
        Canvas.SetTop(trackBg, (h - 4) / 2);
        SeekBarCanvas.Children.Add(trackBg);

        // Filled track
        var trackFill = new System.Windows.Shapes.Rectangle
        {
            Width = w * ratio, Height = 4,
            Fill = Theme.AccentBrush,
            RadiusX = 2, RadiusY = 2
        };
        Canvas.SetLeft(trackFill, 0);
        Canvas.SetTop(trackFill, (h - 4) / 2);
        SeekBarCanvas.Children.Add(trackFill);

        // Knob
        var knob = new System.Windows.Shapes.Ellipse
        {
            Width = 12, Height = 12,
            Fill = Theme.TextBrush
        };
        Canvas.SetLeft(knob, w * ratio - 6);
        Canvas.SetTop(knob, (h - 12) / 2);
        SeekBarCanvas.Children.Add(knob);
    }

    private void SeekBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _seekDragging = true;
        SeekToPosition(e.GetPosition(SeekBarCanvas).X);
        SeekBarCanvas.CaptureMouse();
    }

    private void SeekBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (_seekDragging)
            SeekToPosition(e.GetPosition(SeekBarCanvas).X);
    }

    private void SeekBar_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _seekDragging = false;
        SeekBarCanvas.ReleaseMouseCapture();
    }

    private void SeekToPosition(double x)
    {
        if (_vlcPlayer == null || _vlcPlayer.Length <= 0) return;
        double ratio = Math.Clamp(x / SeekBarCanvas.ActualWidth, 0, 1);
        long newTime = (long)(ratio * _vlcPlayer.Length);

        if (_vlcPlayer.State == VLCState.Ended || _vlcPlayer.State == VLCState.Stopped)
        {
            RestartVideoFrom(ratio);
        }
        else
        {
            _vlcPlayer.Time = newTime;
        }
    }

    private void RestartVideoFrom(double position)
    {
        if (_vlcPlayer == null || _libVlc == null || _videoPath == null) return;
        using var media = new Media(_libVlc, _videoPath, FromType.FromPath);
        _vlcPlayer.Play(media);
        _vlcPlayer.Position = (float)position;
    }

    private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_vlcPlayer == null) return;

        if (_vlcPlayer.State == VLCState.Ended || _vlcPlayer.State == VLCState.Stopped)
        {
            RestartVideoFrom(0);
        }
        else if (_vlcPlayer.State == VLCState.Playing)
        {
            _vlcPlayer.Pause();
        }
        else
        {
            _vlcPlayer.Play();
        }
    }

    private void VideoSurface_Click(object sender, MouseButtonEventArgs e)
    {
        BtnPlayPause_Click(sender, e);
    }

    private void VideoSurface_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_vlcPlayer == null) return;
        var length = _vlcPlayer.Length;
        if (length > 0)
        {
            long skip = length / 100; // 1% per tick
            // Up-roll = rewind, Down-roll = forward
            long newTime = Math.Clamp(_vlcPlayer.Time + (e.Delta > 0 ? -skip : skip), 0, length);
            _vlcPlayer.Time = newTime;
        }
        e.Handled = true;
    }

    private void VideoSkip_Click(object sender, RoutedEventArgs e)
    {
        if (_vlcPlayer == null || sender is not Button btn || btn.Tag is not string tagStr) return;
        if (!int.TryParse(tagStr, out int seconds)) return;

        var length = _vlcPlayer.Length;
        if (length <= 0) return;

        long newTime = Math.Clamp(_vlcPlayer.Time + seconds * 1000L, 0, length);

        if (_vlcPlayer.State == VLCState.Ended || _vlcPlayer.State == VLCState.Stopped)
        {
            RestartVideoFrom((double)newTime / length);
        }
        else
        {
            _vlcPlayer.Time = newTime;
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _videoVolume = (int)e.NewValue;
        if (VolumeText != null) VolumeText.Text = $"{_videoVolume}%";
        if (_vlcPlayer != null) _vlcPlayer.Volume = _videoVolume;
    }

    private void BtnCloseVideo_Click(object sender, RoutedEventArgs e)
    {
        StopVideo();
        _videoPath = null;
        VideoOverlay.Visibility = Visibility.Collapsed;
        EmptyMessage.Visibility = Visibility.Visible;
        RebuildSidebar();
    }

    // ======== KEYBOARD ========

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // Don't handle keys when typing in text boxes
        if (e.OriginalSource is TextBox) return;

        // Ctrl+A: Select all (must check before plain A)
        if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            SelectAll();
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.A:
            case Key.Left:
                if (_viewerOpen)
                    ViewerNavigate(-1);
                else
                    NavigateFile(-1);
                e.Handled = true;
                break;
            case Key.D:
            case Key.Right:
                if (_viewerOpen)
                    ViewerNavigate(1);
                else
                    NavigateFile(1);
                e.Handled = true;
                break;
            case Key.Escape:
                if (_viewerOpen)
                    CloseViewer();
                else
                    BtnClearSelection_Click(sender, e);
                e.Handled = true;
                break;
        }
    }

    private void Window_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        // Mouse side buttons: XButton1 = back, XButton2 = forward
        if (e.ChangedButton == MouseButton.XButton1)
        {
            NavigateFile(-1);
            e.Handled = true;
        }
        else if (e.ChangedButton == MouseButton.XButton2)
        {
            NavigateFile(1);
            e.Handled = true;
        }
    }

    private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_mode == "video") return;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ZoomThumbnails(e.Delta > 0 ? 1 : -1);
            e.Handled = true;
        }
        else if (_viewerOpen)
        {
            ViewerNavigate(e.Delta > 0 ? -1 : 1);
            e.Handled = true;
        }
    }


    // ======== DRAG & DROP ========

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        if (files.Length == 0) return;

        var file = files[0];
        var ext = Path.GetExtension(file).ToLowerInvariant();

        if (_mode == "video" || Theme.VideoExtensions.Contains(ext))
        {
            if (_mode != "video") SwitchMode("video");
            PlayVideo(file);
        }
        else if (Theme.ArchiveExtensions.Contains(ext))
        {
            if (_mode == "video") SwitchMode("browse");
            LoadArchive(file);
        }
    }

    // ======== HELPERS ========

    private void SetStatus(string text)
    {
        TxtStatus.Text = text;
    }

    private void UpdateFileSize(string? path)
    {
        if (path != null && File.Exists(path))
        {
            try { TxtFileSize.Text = FormatSize(new FileInfo(path).Length); return; }
            catch { }
        }
        TxtFileSize.Text = "";
    }

    private void TxtStatus_Click(object sender, MouseButtonEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtStatus.Text))
        {
            Clipboard.SetText(TxtStatus.Text);
        }
    }

    private void ShowError(string message)
    {
        var dlg = new ErrorDialog(message);
        dlg.Owner = this;
        dlg.ShowDialog();
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
    }

    private static string FormatTime(long ms)
    {
        if (ms < 0) ms = 0;
        var ts = TimeSpan.FromMilliseconds(ms);
        return ts.Hours > 0 ? $"{ts:hh\\:mm\\:ss}" : $"{ts:mm\\:ss}";
    }
}
