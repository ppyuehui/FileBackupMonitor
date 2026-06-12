using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FileBackupMonitor.Services;
using Logging;
using Microsoft.Win32;
using MyMessagebox;
using FileBackupMonitor.Models;
using FileBackupMonitor.Views;

namespace FileBackupMonitor.ViewModels
{
    /// <summary>
    /// 文件夹对的 UI 表示
    /// </summary>
    public class FolderPairItem
    {
        public string WatchFolder { get; set; }
        public string BackupFolder { get; set; }
        public string DisplayText => (WatchFolder + "  →  " +  BackupFolder);
    }

    /// <summary>
    /// 文件格式的 UI 表示
    /// </summary>
    public class ExtensionItem : INotifyPropertyChanged
    {
        private bool _isMonitored;
        public string Extension { get; set; }
        public int Count { get; set; }
        public bool IsMonitored
        {
            get => _isMonitored;
            set { if (_isMonitored != value) { _isMonitored = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMonitored))); } }
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly Models.AppSettings _original;
        private readonly LogService _logService;
        public Models.AppSettings Settings { get; }

        private string _ignorePatternsText;
        public string IgnorePatternsText
        {
            get => _ignorePatternsText;
            set { _ignorePatternsText = value; OnPropertyChanged(); }
        }

        private string _ignoredFoldersText;
        public string IgnoredFoldersText
        {
            get => _ignoredFoldersText;
            set { _ignoredFoldersText = value; OnPropertyChanged(); }
        }

        /// <summary>文件夹对列表（用于 UI 绑定）</summary>
        public ObservableCollection<FolderPairItem> FolderPairs { get; } = new ObservableCollection<FolderPairItem>();

        private FolderPairItem _selectedPair;
        public FolderPairItem SelectedPair
        {
            get => _selectedPair;
            set { _selectedPair = value; OnPropertyChanged(); }
        }

        public ICommand AddPairCommand { get; }
        public ICommand RemovePairCommand { get; }
        public ICommand ClearAllPairsCommand { get; }
        public ICommand BatchImportCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ScanExtensionsCommand { get; }
        public ICommand ToggleAllExtensionsCommand { get; }
        public ICommand SortByCheckedCommand { get; }
        public ICommand SortByExtensionCommand { get; }
        public ICommand ApplyCategoryCommand { get; }
        public ICommand EditCategoryCommand { get; }
        public ICommand RestoreDefaultExcludeCommand { get; }
        public ICommand ClearIncludeCommand { get; }

        private static readonly List<string> DefaultExcludePatterns = new List<string>
        {
            "*.temp", "*.tmp", "*.tgz", "*.his", "*.for", "*.appdf", "*.def",
            "*.sor", "*.sin", "*.scr", "*.rp", "*.prd", "*.nol", "*.nin",
            "*.jnl", "*.itd", "*.inx", "*.inm", "*.in", "*.ikc", "*.gen",
            "*.dyl", "*.dfm", "*.cod", "*.apprj", "*.co1", "*.co2", "*.edf",
            "*.nis", "*.ia1", "*.sum", "*.bks", "*.ia2", "*.ads", "*.dwl",
            "*.dwl2", "*.adv", "*.plf", "*.rp1", "*.rp2", "*.in0", "*.msh",
            "*.odi", "*.bk$", "*.bak",
            "*.asd", "*.xlk", "*.wbk", "~$*",
            "*.dll", "*.exe", "*.pdb", "*.pyc", "*.class", "*.o", "*.obj",
            "Thumbs.db", ".DS_Store",
        };

        private string _activeCategories = "";
        public string ActiveCategories
        {
            get => _activeCategories;
            set { _activeCategories = value; OnPropertyChanged(); }
        }

        private bool _isExcludeMode = true;
        public bool IsExcludeMode
        {
            get => _isExcludeMode;
            set
            {
                if (_isExcludeMode == value) return;
                SaveCurrentPatternsToList();
                _isExcludeMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsIncludeMode));
                LoadPatternsFromList();
            }
        }
        public bool IsIncludeMode
        {
            get => !_isExcludeMode;
            set
            {
                var newExclude = !value;
                if (_isExcludeMode == newExclude) return;
                SaveCurrentPatternsToList();
                _isExcludeMode = newExclude;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsExcludeMode));
                LoadPatternsFromList();
            }
        }

        /// <summary>将当前 IgnorePatternsText 保存到对应模式的列表</summary>
        private void SaveCurrentPatternsToList()
        {
            var list = ParsePatterns(IgnorePatternsText);
            if (_isExcludeMode)
                _excludePatterns = list;
            else
                _includePatterns = list;
        }

        /// <summary>从当前模式的列表加载到 IgnorePatternsText</summary>
        private void LoadPatternsFromList()
        {
            var list = _isExcludeMode ? _excludePatterns : _includePatterns;
            IgnorePatternsText = string.Join(", ", list);
        }

        /// <summary>解析逗号分隔的模式文本</summary>
        private static List<string> ParsePatterns(string text)
        {
            return (text ?? "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();
        }

        /// <summary>分类定义（可编辑）</summary>
        private readonly Dictionary<string, List<string>> _categoryDefinitions = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["文档"] = new List<string> { ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf", ".txt", ".rtf", ".csv", ".odt", ".ods", ".odp", ".wps", ".et", ".dps", ".md", ".log", ".ini", ".cfg", ".conf", ".properties" },
            ["图片"] = new List<string> { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".tiff", ".tif", ".webp", ".svg", ".psd", ".ai", ".raw", ".cr2", ".nef", ".heic", ".heif", ".eps", ".emf", ".wmf", ".pcx", ".tga", ".dds" },
            ["源码"] = new List<string> { ".cs", ".vb", ".js", ".ts", ".py", ".java", ".c", ".cpp", ".h", ".hpp", ".csproj", ".sln", ".xaml", ".xml", ".json", ".yaml", ".yml", ".html", ".css", ".sql", ".sh", ".bat", ".ps1", ".go", ".rs", ".swift", ".kt", ".rb", ".php", ".vue", ".jsx", ".tsx", ".scss", ".less", ".sass" },
            ["压缩包"] = new List<string> { ".zip", ".rar", ".7z", ".iso" },
            ["可执行文件"] = new List<string> { ".msi", ".com", ".cmd" },
            ["音视频"] = new List<string> { ".mp3", ".mp4", ".wav", ".avi", ".mkv", ".flac", ".aac", ".ogg", ".wma", ".mov", ".wmv", ".flv", ".m4a", ".m4v", ".webm", ".3gp", ".rmvb", ".ts" },
            ["设计软件"] = new List<string> { ".psd", ".ai", ".sketch", ".dwg", ".dxf", ".rft" },
            ["工程软件"] = new List<string> { ".apw", ".apwz", ".prz", ".dwg", ".htri", ".edr", ".eddx", ".sulcol", ".kgt" },
            ["辉哥软件"] = new List<string> { ".hui", ".huix", ".huij", ".huiw", ".az", ".rs" },
            ["数据库"] = new List<string> { ".db", ".sqlite", ".sqlite3", ".mdb", ".accdb", ".sql", ".mdf", ".ldf", ".dbf", ".gdb" },
            ["临时文件"] = new List<string> { ".cache", ".swp", ".old", ".orig", ".sav", ".autosave" },
        };

        private string _batchInputText = "";
        public string BatchInputText
        {
            get => _batchInputText;
            set { _batchInputText = value; OnPropertyChanged(); }
        }

        private string _batchImportStatus = "";
        public string BatchImportStatus
        {
            get => _batchImportStatus;
            set { _batchImportStatus = value; OnPropertyChanged(); }
        }

        /// <summary>扫描到的文件格式列表</summary>
        public ObservableCollection<ExtensionItem> Extensions { get; } = new ObservableCollection<ExtensionItem>();

        private bool _allExtensionsChecked = true;
        public bool AllExtensionsChecked
        {
            get => _allExtensionsChecked;
            set { _allExtensionsChecked = value; OnPropertyChanged(); }
        }

        private string _extensionScanStatus;
        public string ExtensionScanStatus
        {
            get => _extensionScanStatus;
            set { _extensionScanStatus = value; OnPropertyChanged(); }
        }

        private readonly HashSet<string> _activeCategorySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>排除模式下的扩展名列表</summary>
        private List<string> _excludePatterns;
        /// <summary>包含模式下的扩展名列表</summary>
        private List<string> _includePatterns;

        public SettingsViewModel(Models.AppSettings source, LogService logService = null)
        {
            _original = source;
            _logService = logService;

            Settings = new Models.AppSettings
            {
                FolderPairs = new List<FolderPair>(),
                DelaySeconds = source.DelaySeconds,
                RetentionDays = source.RetentionDays,
                MinimizeToTray = source.MinimizeToTray,
                IncludeSubfolders = source.IncludeSubfolders,
                IsDarkTheme = source.IsDarkTheme,
                FilterMode = source.FilterMode,
                IgnorePatterns = new List<string>(source.IgnorePatterns),
                IncludePatterns = new List<string>(source.IncludePatterns),
                IgnoredFolders = new List<string>(source.IgnoredFolders),
                RecentLogCount = source.RecentLogCount,
                StartWithWindows = source.StartWithWindows,
                LogMaxSizeMB = source.LogMaxSizeMB,
                LogRetentionDays = source.LogRetentionDays,
                MaxBackupLogs = source.MaxBackupLogs
            };

            // 初始化两套扩展名列表
            _excludePatterns = new List<string>(source.IgnorePatterns);
            _includePatterns = new List<string>(source.IncludePatterns);

            // 先设置模式，再加载文本（避免 setter 中的 early return 导致数据丢失）
            _isExcludeMode = source.FilterMode == Models.FilterMode.Exclude;

            foreach (var p in source.FolderPairs)
            {
                Settings.FolderPairs.Add(new FolderPair { WatchFolder = p.WatchFolder, BackupFolder = p.BackupFolder });
                FolderPairs.Add(new FolderPairItem { WatchFolder = p.WatchFolder, BackupFolder = p.BackupFolder });
            }

            // 根据当前模式加载对应的扩展名文本
            var currentPatterns = IsExcludeMode ? _excludePatterns : _includePatterns;
            IgnorePatternsText = string.Join(", ", currentPatterns);
            IgnoredFoldersText = string.Join(", ", Settings.IgnoredFolders);

            // 确保两个属性都触发通知，让 RadioButton 正确选中
            OnPropertyChanged(nameof(IsExcludeMode));
            OnPropertyChanged(nameof(IsIncludeMode));

            // 检查并修正开机自启注册表路径
            SyncAutoStartRegistry();

            // 添加一对
            AddPairCommand = new RelayCommand(_ =>
            {
                var watch = BrowseFolder("选择监控文件夹");
                if (watch == null) return;
                var backup = BrowseFolder("选择对应的备份文件夹");
                if (backup == null) return;

                // 检查不能相同
                if (string.Equals(watch, backup, StringComparison.OrdinalIgnoreCase))
                {
                    MyMessageBox.Show("监控文件夹和备份文件夹不能相同", "提示");
                    return;
                }
                // 检查重复
                foreach (var p in FolderPairs)
                    if (string.Equals(p.WatchFolder, watch, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(p.BackupFolder, backup, StringComparison.OrdinalIgnoreCase))
                    { MyMessageBox.Show("这对文件夹已存在", "提示"); return; }

                FolderPairs.Add(new FolderPairItem { WatchFolder = watch, BackupFolder = backup });
            });

            // 删除选中的一对
            RemovePairCommand = new RelayCommand(_ =>
            {
                if (SelectedPair != null) FolderPairs.Remove(SelectedPair);
            }, _ => SelectedPair != null);

            // 批量导入
            BatchImportCommand = new RelayCommand(_ =>
            {
                var text = (BatchInputText ?? "").Trim();
                if (string.IsNullOrEmpty(text))
                { BatchImportStatus = "请先输入内容"; return; }

                int added = 0, skipped = 0, errors = 0;
                var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("//")) continue;

                    // 支持多种分隔符: | 、→ 、-> 、\t（Tab）
                    string[] parts = null;
                    if (line.Contains("|")) parts = line.Split(new[] { '|' }, 2);
                    else if (line.Contains("→")) parts = line.Split(new[] { '→' }, 2);
                    else if (line.Contains("->")) parts = line.Split(new[] { "->" }, 2, StringSplitOptions.None);
                    else if (line.Contains("\t")) parts = line.Split(new[] { '\t' }, 2);

                    if (parts == null || parts.Length < 2) { errors++; continue; }

                    var watch = parts[0].Trim().Trim('"');
                    var backup = parts[1].Trim().Trim('"');

                    if (string.IsNullOrWhiteSpace(watch) || string.IsNullOrWhiteSpace(backup))
                    { errors++; continue; }

                    if (string.Equals(watch, backup, StringComparison.OrdinalIgnoreCase))
                    { skipped++; continue; }

                    // 检查重复
                    bool dup = false;
                    foreach (var p in FolderPairs)
                        if (string.Equals(p.WatchFolder, watch, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(p.BackupFolder, backup, StringComparison.OrdinalIgnoreCase))
                        { dup = true; break; }
                    if (dup) { skipped++; continue; }

                    FolderPairs.Add(new FolderPairItem { WatchFolder = watch, BackupFolder = backup });
                    added++;
                }

                BatchImportStatus = added > 0
                    ? $"✅ 成功导入 {added} 对" + (skipped > 0 ? $"，跳过 {skipped} 对" : "") + (errors > 0 ? $"，{errors} 行格式错误" : "")
                    : (errors > 0 ? $"❌ {errors} 行格式错误，请检查" : "没有新内容导入");
            });

            // 扫描文件格式（异步）
            ScanExtensionsCommand = new RelayCommand(_ =>
            {
                Extensions.Clear();
                ExtensionScanStatus = "正在扫描...";
                var pairs = Settings.FolderPairs.ToList();

                Task.Run(() =>
                {
                    var allExtensions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (var pair in pairs)
                    {
                        var watchFolder = pair.WatchFolder;
                        if (string.IsNullOrWhiteSpace(watchFolder) || !Directory.Exists(watchFolder)) continue;
                        try
                        {
                            foreach (var file in Directory.EnumerateFiles(watchFolder, "*", SearchOption.AllDirectories))
                            {
                                try
                                {
                                    var ext = Path.GetExtension(file).ToLowerInvariant();
                                    if (string.IsNullOrEmpty(ext)) ext = "(无扩展名)";
                                    if (allExtensions.ContainsKey(ext)) allExtensions[ext]++;
                                    else allExtensions[ext] = 1;
                                }
                                catch (Exception ex) { FileLogger.LogError("获取文件扩展名失败: " + file, ex); }
                            }
                        }
                        catch (Exception ex) { FileLogger.LogError("扫描文件夹失败: " + watchFolder, ex); }
                    }

                var ignoredExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var scanPatterns = _isExcludeMode ? _excludePatterns : _includePatterns;
                foreach (var pattern in scanPatterns)
                {
                    var t = pattern.Trim();
                    if (t.StartsWith("*.")) ignoredExts.Add(t.Substring(1));
                    else if (t.StartsWith("*")) ignoredExts.Add(t.Substring(1));
                }

                var result = allExtensions
                    .OrderByDescending(x => x.Value)
                    .Select(x => new ExtensionItem
                    {
                        Extension = x.Key,
                        Count = x.Value,
                        IsMonitored = _isExcludeMode
                            ? !ignoredExts.Contains(x.Key) && !ignoredExts.Contains("*" + x.Key)
                            : ignoredExts.Contains(x.Key) || ignoredExts.Contains("*" + x.Key)
                    })
                    .ToList();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var item in result)
                        {
                            item.PropertyChanged += ExtensionItem_PropertyChanged;
                            Extensions.Add(item);
                        }
                        ExtensionScanStatus = $"共扫描到 {allExtensions.Count} 种格式";
                    });
                });
            });

            // 全选/全不选
            ToggleAllExtensionsCommand = new RelayCommand(_ =>
            {
                foreach (var item in Extensions)
                    item.IsMonitored = AllExtensionsChecked;
            });

            // 按是否勾选排序
            SortByCheckedCommand = new RelayCommand(_ =>
            {
                var sorted = Extensions.OrderBy(x => x.IsMonitored).ToList();
                Extensions.Clear();
                foreach (var item in sorted) Extensions.Add(item);
            });

            // 按扩展名排序
            SortByExtensionCommand = new RelayCommand(_ =>
            {
                var sorted = Extensions.OrderBy(x => x.Extension, StringComparer.OrdinalIgnoreCase).ToList();
                Extensions.Clear();
                foreach (var item in sorted) Extensions.Add(item);
            });

            // 分类快捷勾选（切换：再点一次取消）
            ApplyCategoryCommand = new RelayCommand(param =>
            {
                var category = param as string;
                if (string.IsNullOrEmpty(category)) return;
                if (!_categoryDefinitions.ContainsKey(category)) return;
                var target = new HashSet<string>(_categoryDefinitions[category], StringComparer.OrdinalIgnoreCase);

                bool activate = !_activeCategorySet.Contains(category);
                if (activate) _activeCategorySet.Add(category);
                else _activeCategorySet.Remove(category);

                ActiveCategories = string.Join(",", _activeCategorySet);

                foreach (var item in Extensions)
                {
                    if (target.Contains(item.Extension))
                        item.IsMonitored = activate;
                }
            });

            // 右键编辑分类扩展名
            EditCategoryCommand = new RelayCommand(param =>
            {
                var category = param as string;
                if (string.IsNullOrEmpty(category)) return;
                if (!_categoryDefinitions.ContainsKey(category)) return;

                var currentExts = _categoryDefinitions[category];
                var input = string.Join(", ", currentExts);
                var dialog = new InputDialog($"编辑「{category}」的扩展名", "用逗号分隔，例如: .doc, .docx, .pdf", input);
                if (dialog.ShowDialog() == true)
                {
                    var newExts = dialog.InputText
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim().ToLowerInvariant())
                        .Where(x => !string.IsNullOrEmpty(x))
                        .Select(x => x.StartsWith(".") ? x : "." + x)
                        .Distinct()
                        .ToList();
                    _categoryDefinitions[category] = newExts;
                }
            });

            // 恢复默认排除列表
            RestoreDefaultExcludeCommand = new RelayCommand(_ =>
            {
                _excludePatterns = new List<string>(DefaultExcludePatterns);
                IgnorePatternsText = string.Join(", ", _excludePatterns);
                SyncExtensionsCheckState();
            });

            // 清空包含列表
            ClearIncludeCommand = new RelayCommand(_ =>
            {
                _includePatterns = new List<string>();
                IgnorePatternsText = "";
                SyncExtensionsCheckState();
            });

            SaveCommand = new RelayCommand(win =>
            {
                // 同步扩展名到当前模式的列表
                if (Extensions.Count > 0) SyncExtensionsToIgnorePatterns();

                // 保存当前文本到对应模式的列表
                SaveCurrentPatternsToList();

                // 同步 FilterMode 和两套列表到 Settings
                Settings.FilterMode = IsExcludeMode ? Models.FilterMode.Exclude : Models.FilterMode.Include;
                Settings.IgnorePatterns = new List<string>(_excludePatterns);
                Settings.IncludePatterns = new List<string>(_includePatterns);

                // 同步到 Settings
                Settings.FolderPairs.Clear();
                foreach (var item in FolderPairs)
                    Settings.FolderPairs.Add(new FolderPair { WatchFolder = item.WatchFolder, BackupFolder = item.BackupFolder });

                // 解析忽略文件夹路径
                Settings.IgnoredFolders.Clear();
                var folderParts = (IgnoredFoldersText ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in folderParts) { var t = p.Trim(); if (!string.IsNullOrEmpty(t)) Settings.IgnoredFolders.Add(t); }

                // 验证
                if (Settings.FolderPairs.Count == 0)
                { MyMessageBox.Show("请至少添加一对监控/备份文件夹", "提示"); return; }

                foreach (var p in Settings.FolderPairs)
                {
                    if (string.IsNullOrWhiteSpace(p.WatchFolder))
                    { MyMessageBox.Show("监控文件夹不能为空", "提示"); return; }
                    if (string.IsNullOrWhiteSpace(p.BackupFolder))
                    { MyMessageBox.Show("备份文件夹不能为空", "提示"); return; }
                    if (string.Equals(p.WatchFolder, p.BackupFolder, StringComparison.OrdinalIgnoreCase))
                    { MyMessageBox.Show($"监控和备份文件夹不能相同:\n{p.WatchFolder}", "提示"); return; }
                }

                try
                {
                    var exe = System.Reflection.Assembly.GetEntryAssembly()?.Location;
                    if (!string.IsNullOrEmpty(exe))
                    {
                        using (var rk = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                        {
                            if (rk != null)
                            {
                                if (Settings.StartWithWindows) rk.SetValue("文件备份监控助手", "\"" + exe + "\"");
                                else rk.DeleteValue("文件备份监控助手", false);
                            }
                        }                       
                    }
                }
                catch (Exception ex) { FileLogger.LogError("设置自启失败", ex); }

                var window = win as Window;
                if (window != null) { window.DialogResult = true; window.Close(); }
            });

            CancelCommand = new RelayCommand(win => { (win as Window)?.Close(); });
        }

        public int MaxBackupLogs
        {
            get => Settings.MaxBackupLogs;
            set { if (Settings.MaxBackupLogs != value) { Settings.MaxBackupLogs = value; _logService?.PerformMaintenance(); OnPropertyChanged(); } }
        }

        public int RecentLogCount
        {
            get => Settings.RecentLogCount;
            set { if (Settings.RecentLogCount != value) { Settings.RecentLogCount = value; OnPropertyChanged(); } }
        }

        public static string BrowseFolder(string description = "请选择文件夹")
        {
            var dialog = new OpenFileDialog { Title = description, Filter = "文件夹|.*", CheckFileExists = false, CheckPathExists = true, FileName = "选择文件夹" };
            if (dialog.ShowDialog() == true)
                return Path.GetDirectoryName(dialog.FileName);
            return null;
        }

        /// <summary>
        /// 检查并修正开机自启注册表路径，确保路径与当前 exe 一致
        /// </summary>
        private void SyncAutoStartRegistry()
        {
            try
            {
                var currentExe = System.Reflection.Assembly.GetEntryAssembly()?.Location;
                if (string.IsNullOrEmpty(currentExe)) return;

                const string regKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
                const string regValueName = "文件备份监控助手";

                using (var rk = Registry.CurrentUser.OpenSubKey(regKeyPath, true))
                {
                    if (rk == null) return;

                    var existingValue = rk.GetValue(regValueName) as string;
                    var expectedValue = "\"" + currentExe + "\"";

                    // 注册表里记录的是哪个 exe 路径开了自启
                    // 当前 exe 路径与注册表匹配 → 勾选；不匹配 → 取消勾选
                    if (string.Equals(existingValue, expectedValue, StringComparison.OrdinalIgnoreCase))
                    {
                        Settings.StartWithWindows = true;
                    }
                    else
                    {
                        Settings.StartWithWindows = false;
                    }
                }
            }
            catch (Exception ex) { FileLogger.LogError("检查开机自启注册表失败", ex); }
        }

        private void ExtensionItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ExtensionItem.IsMonitored))
                SyncExtensionsToIgnorePatterns();
        }

        private void SyncExtensionsToIgnorePatterns()
        {
            var currentList = _isExcludeMode ? _excludePatterns : _includePatterns;

            // 扫描到的所有扩展名（带 * 前缀）
            var scannedExts = new HashSet<string>(
                Extensions.Select(x => "*" + x.Extension),
                StringComparer.OrdinalIgnoreCase);

            // 1. 保留列表中不属于扫描范围的规则（用户手动输入的通配符等）
            var preserved = currentList.Where(p => !scannedExts.Contains(p.Trim())).ToList();

            // 2. 为扫描到的扩展名按当前勾选状态生成规则，保持扫描顺序
            var extRules = Extensions
                .Where(x => (_isExcludeMode && !x.IsMonitored) || (!_isExcludeMode && x.IsMonitored))
                .Select(x => "*" + x.Extension)
                .ToList();

            // 3. 合并：保留的手动规则在前，扩展名规则在后
            var newList = new List<string>();
            newList.AddRange(preserved);
            newList.AddRange(extRules);

            // 更新原始列表
            currentList.Clear();
            currentList.AddRange(newList);

            // 同步到文本框
            IgnorePatternsText = string.Join(", ", currentList);
        }

        /// <summary>根据当前模式的列表同步扩展名勾选框状态</summary>
        private void SyncExtensionsCheckState()
        {
            if (Extensions.Count == 0) return;

            var currentList = _isExcludeMode ? _excludePatterns : _includePatterns;
            var patternExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in currentList)
            {
                var t = p.Trim();
                if (t.StartsWith("*.")) patternExts.Add(t.Substring(1));
                else if (t.StartsWith("*")) patternExts.Add(t.Substring(1));
            }

            foreach (var item in Extensions)
            {
                item.IsMonitored = _isExcludeMode
                    ? !patternExts.Contains(item.Extension) && !patternExts.Contains("*" + item.Extension)
                    : patternExts.Contains(item.Extension) || patternExts.Contains("*" + item.Extension);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
