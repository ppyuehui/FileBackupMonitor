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

namespace FileBackupMonitor.ViewModels
{
    /// <summary>
    /// 文件夹对的 UI 表示
    /// </summary>
    public class FolderPairItem : INotifyPropertyChanged
    {
        public string WatchFolder { get; set; }
        public string BackupFolder { get; set; }
        public string DisplayText => (WatchFolder + "  →  " +  BackupFolder);
        public event PropertyChangedEventHandler PropertyChanged;
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

        public string _ignorePatternsText;
        public string IgnorePatternsText
        {
            get => _ignorePatternsText;
            set { _ignorePatternsText = value; OnPropertyChanged(); }
        }

        public string _ignoredFoldersText;
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

        private string _activeCategories = "";
        public string ActiveCategories
        {
            get => _activeCategories;
            set { _activeCategories = value; OnPropertyChanged(); }
        }

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

        private string _lastClickedCategory = "";
        private readonly HashSet<string> _activeCategorySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                IgnorePatterns = new List<string>(source.IgnorePatterns),
                IgnoredFolders = new List<string>(source.IgnoredFolders),
                RecentLogCount = source.RecentLogCount,
                StartWithWindows = source.StartWithWindows,
                LogMaxSizeMB = source.LogMaxSizeMB,
                LogRetentionDays = source.LogRetentionDays,
                MaxBackupLogs = source.MaxBackupLogs
            };

            foreach (var p in source.FolderPairs)
            {
                Settings.FolderPairs.Add(new FolderPair { WatchFolder = p.WatchFolder, BackupFolder = p.BackupFolder });
                FolderPairs.Add(new FolderPairItem { WatchFolder = p.WatchFolder, BackupFolder = p.BackupFolder });
            }

            IgnorePatternsText = string.Join(", ", Settings.IgnorePatterns);
            IgnoredFoldersText = string.Join(", ", Settings.IgnoredFolders);

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

            //// 清空全部
            //ClearAllPairsCommand = new RelayCommand(_ =>
            //{
            //    if (FolderPairs.Count == 0) return;
            //    if (MyMessageBox.Show($"确定清空全部 {FolderPairs.Count} 对文件夹？", "确认") == true)
            //        FolderPairs.Clear();
            //}, _ => FolderPairs.Count > 0);

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
                                catch { }
                            }
                        }
                        catch { }
                    }

                    var ignoredExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var pattern in Settings.IgnorePatterns)
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
                            IsMonitored = !ignoredExts.Contains(x.Key) && !ignoredExts.Contains("*" + x.Key)
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

                var categories = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["文档"] = new HashSet<string> { ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf", ".txt", ".rtf", ".csv", ".odt", ".ods", ".odp", ".wps", ".et", ".dps" },
                    ["图片"] = new HashSet<string> { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".tiff", ".tif", ".webp", ".svg", ".psd", ".ai", ".raw", ".cr2", ".nef", ".heic", ".heif" },
                    ["源码"] = new HashSet<string> { ".cs", ".vb", ".js", ".ts", ".py", ".java", ".c", ".cpp", ".h", ".hpp", ".csproj", ".sln", ".xaml", ".xml", ".json", ".yaml", ".yml", ".html", ".css", ".sql", ".sh", ".bat", ".ps1" },
                    ["压缩包"] = new HashSet<string> { ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".tgz", ".iso" },
                    ["可执行文件"] = new HashSet<string> { ".exe", ".dll", ".msi", ".com", ".bat", ".cmd", ".scr" },
                    ["音视频"] = new HashSet<string> { ".mp3", ".mp4", ".wav", ".avi", ".mkv", ".flac", ".aac", ".ogg", ".wma", ".mov", ".wmv", ".flv" },
                    ["常用设计软件"] = new HashSet<string> { ".apw", ".apwz", ".prz", ".psd", ".ai", ".indd", ".sketch", ".fig", ".xd", ".dwg", ".dxf", ".rvt", ".rfa", ".rte", ".rft", ".plf", ".edp", ".edf", ".nis", ".sin" },
                    ["临时文件"] = new HashSet<string> { ".tmp", ".temp", ".bak", ".log", ".cache", ".swp" },
                };

                if (!categories.ContainsKey(category)) return;
                var target = categories[category];

                // 切换：如果该分类已激活则取消，否则激活
                bool activate = !_activeCategorySet.Contains(category);
                if (activate)
                    _activeCategorySet.Add(category);
                else
                    _activeCategorySet.Remove(category);

                // 更新 ActiveCategories 字符串（触发按钮颜色变化）
                ActiveCategories = string.Join(",", _activeCategorySet);

                foreach (var item in Extensions)
                {
                    if (target.Contains(item.Extension))
                        item.IsMonitored = activate;
                }
            });

            SaveCommand = new RelayCommand(win =>
            {
                // 同步扩展名到忽略模式
                if (Extensions.Count > 0) SyncExtensionsToIgnorePatterns();

                // 同步到 Settings
                Settings.FolderPairs.Clear();
                foreach (var item in FolderPairs)
                    Settings.FolderPairs.Add(new FolderPair { WatchFolder = item.WatchFolder, BackupFolder = item.BackupFolder });

                // 解析忽略通配符
                Settings.IgnorePatterns.Clear();
                var parts = (IgnorePatternsText ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in parts) { var t = p.Trim(); if (!string.IsNullOrEmpty(t)) Settings.IgnorePatterns.Add(t); }

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

        private void ExtensionItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ExtensionItem.IsMonitored))
                SyncExtensionsToIgnorePatterns();
        }

        private void SyncExtensionsToIgnorePatterns()
        {
            // 从 IgnorePatterns 中移除所有扫描到的扩展名规则
            var scannedExts = new HashSet<string>(Extensions.Select(x => x.Extension), StringComparer.OrdinalIgnoreCase);
            Settings.IgnorePatterns.RemoveAll(p =>
            {
                var t = p.Trim();
                if (t.StartsWith("*."))
                    return scannedExts.Contains(t.Substring(1));
                if (t.StartsWith("*"))
                    return scannedExts.Contains(t.Substring(1));
                return false;
            });

            // 添加未监控的扩展名
            foreach (var item in Extensions.Where(x => !x.IsMonitored))
            {
                var pattern = "*" + item.Extension;
                if (!Settings.IgnorePatterns.Any(p => p.Trim().Equals(pattern, StringComparison.OrdinalIgnoreCase)))
                    Settings.IgnorePatterns.Add(pattern);
            }

            // 同步到文本框
            IgnorePatternsText = string.Join(", ", Settings.IgnorePatterns);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
