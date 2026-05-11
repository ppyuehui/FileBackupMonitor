using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using FileBackupMonitor.Models;
using FileBackupMonitor.Services;
using Logging;
using MyMessagebox;
using FileBackupMonitor.Views;

namespace FileBackupMonitor.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private BackupService _service;
        private Models.AppSettings _settings;
        private bool _isMonitoring;
        private Services.LogService _logService;
        private System.Threading.Timer _maintenanceTimer;

        public ObservableCollection<BackupLogEntry> Logs { get; } = new ObservableCollection<BackupLogEntry>();

        private bool _uiVisible = true;
        public bool UiVisible
        {
            get { return _uiVisible; }
            set
            {
                if (_uiVisible == value) return;
                _uiVisible = value;
                OnPropertyChanged();
                if (!_uiVisible) { Logs.Clear(); }
                else { LoadRecentLogs(); }
            }
        }

        public bool IsMonitoring
        {
            get { return _isMonitoring; }
            private set { _isMonitoring = value; OnPropertyChanged(); }
        }

        /// <summary>监控文件夹显示文本</summary>
        public string WatchFoldersText
        {
            get
            {
                var pairs = _settings?.FolderPairs ?? new System.Collections.Generic.List<FolderPair>();
                return pairs.Count > 0
                    ? string.Join(" | ", pairs.Select(p => Path.GetFileName(p.WatchFolder) ?? p.WatchFolder))
                    : "未设置";
            }
        }

        /// <summary>备份文件夹显示文本</summary>
        public string BackupFoldersText
        {
            get
            {
                var pairs = _settings?.FolderPairs ?? new System.Collections.Generic.List<FolderPair>();
                return pairs.Count > 0
                    ? string.Join(" | ", pairs.Select(p => Path.GetFileName(p.BackupFolder) ?? p.BackupFolder))
                    : "未设置";
            }
        }

        /// <summary>文件夹对显示列表（供 ItemsControl 绑定）</summary>
        public System.Collections.Generic.List<dynamic> FolderPairDisplayList
        {
            get
            {
                var pairs = _settings?.FolderPairs ?? new System.Collections.Generic.List<FolderPair>();
                return pairs.Select(p => (dynamic)new
                {
                    WatchFolder = p.WatchFolder,
                    BackupFolder = p.BackupFolder
                }).ToList();
            }
        }

        // 兼容旧绑定
        public string WatchFolder => _settings?.WatchFolder ?? string.Empty;
        public string BackupFolder => _settings?.BackupFolder ?? string.Empty;

        public string StatusText
        {
            get
            {
                if (!IsMonitoring) return "⏹️ 已停止";
                var count = (_settings?.FolderPairs ?? new System.Collections.Generic.List<FolderPair>()).Count;
                return $"🟢 监控中 — {count} 对文件夹";
            }
        }

        private string _statsText = "";
        public string StatsText { get { return _statsText; } set { _statsText = value; OnPropertyChanged(); } }

        private int _totalBackups;
        public int TotalBackups { get { return _totalBackups; } set { _totalBackups = value; OnPropertyChanged(); OnPropertyChanged("StatsText"); } }

        private long _totalSize;
        public long TotalSize
        {
            get { return _totalSize; }
            set { _totalSize = value; OnPropertyChanged(); StatsText = string.Format("已备份 {0} 个文件  |  占用 {1}", TotalBackups, FormatSize(TotalSize)); }
        }

        public ICommand OpenLogFolderCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand LoadRecentLogsCommand { get; }
        public ICommand OpenWatchFolderCommand { get; }
        public ICommand OpenBackupFolderCommand { get; }
        public ICommand ClearLogsCommand { get; }
        public ICommand ManualCleanupCommand { get; }
        public ICommand OpenFileCommand { get; }
        public ICommand OpenFileLocationCommand { get; }
        public ICommand OpenOriginalLocationCommand { get; private set; }
        public ICommand OpenErrorLogFolderCommand { get; }
        public ICommand ThemeToggleCommand { get; }
        public ICommand OpenLogWindowCommand { get; }

        public bool IsDarkTheme => _settings.IsDarkTheme;
        public string ThemeToggleText => _settings.IsDarkTheme ? "☀️" : "🌙";
        public bool MinimizeToTray => _settings?.MinimizeToTray ?? true;

        public MainViewModel()
        {
            _settings = SettingsService.Load();
            InitializeService();
            _logService = new Services.LogService(_settings);
            EnsureMaintenanceTimer();

            try { var s = _logService.GetSummary(); TotalBackups = s.Count; TotalSize = s.TotalSize; }
            catch (Exception ex) { FileLogger.LogError("加载摘要失败", ex); }

            StartCommand = new RelayCommand(_ => Start(), _ => !IsMonitoring && (_settings.FolderPairs?.Count ?? 0) > 0);
            OpenLogFolderCommand = new RelayCommand(_ => OpenLogFolder());
            StopCommand = new RelayCommand(_ => Stop(), _ => IsMonitoring);
            OpenLogWindowCommand = new RelayCommand(_ => OpenLogWindow());
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
            LoadRecentLogsCommand = new RelayCommand(_ => LoadRecentLogs());
            OpenWatchFolderCommand = new RelayCommand(_ =>
            {
                foreach (var p in _settings.FolderPairs)
                    if (Directory.Exists(p.WatchFolder)) OpenFolder(p.WatchFolder);
            }, _ => (_settings.FolderPairs?.Count ?? 0) > 0);
            OpenBackupFolderCommand = new RelayCommand(_ =>
            {
                foreach (var p in _settings.FolderPairs)
                    if (Directory.Exists(p.BackupFolder)) OpenFolder(p.BackupFolder);
            }, _ => (_settings.FolderPairs?.Count ?? 0) > 0);
            ClearLogsCommand = new RelayCommand(_ => { Logs.Clear(); _logService.Clear(); TotalBackups = 0; TotalSize = 0; });
            ManualCleanupCommand = new RelayCommand(_ => _service.CleanupOldBackups());
            OpenFileCommand = new RelayCommand(obj => OpenFile(obj as BackupLogEntry));
            OpenFileLocationCommand = new RelayCommand(obj => OpenFileLocation(obj as BackupLogEntry));
            OpenOriginalLocationCommand = new RelayCommand(obj => OpenOriginalLocation(obj as BackupLogEntry));
            OpenErrorLogFolderCommand = new RelayCommand(_ =>
            {
                var errorLogDir = Path.Combine(Path.GetTempPath(), "文件备份监控助手" + "log");
                OpenFolder(errorLogDir);
            });
            ThemeToggleCommand = new RelayCommand(_ =>
            {
                _settings.IsDarkTheme = !_settings.IsDarkTheme;
                App.ApplyTheme(_settings.IsDarkTheme);
                SettingsService.Save(_settings);
                OnPropertyChanged(nameof(IsDarkTheme));
                OnPropertyChanged(nameof(ThemeToggleText));
            });

            Start();
        }

        private void OpenOriginalLocation(object obj)
        {
            var log = obj as BackupLogEntry;
            if (log == null) return;
            try
            {
                // 优先用绝对路径，找不到则回退遍历 FolderPairs
                if (!string.IsNullOrEmpty(log.SourceFullPath))
                {
                    var dir = Path.GetDirectoryName(log.SourceFullPath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    { Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true, Verb = "open" }); return; }
                }
                //// 回退：用 WatchFolder + RelativePath 拼接
                //if (!string.IsNullOrEmpty(log.WatchFolder))
                //{
                //    var path = Path.Combine(log.WatchFolder, log.RelativePath);
                //    var dir = Path.GetDirectoryName(path);
                //    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                //    { Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true, Verb = "open" }); return; }
                //}
                // 最后回退到备份路径
                //var d = Path.GetDirectoryName(log.BackupPath);
                //if (!string.IsNullOrEmpty(d) && Directory.Exists(d))
                //    Process.Start(new ProcessStartInfo { FileName = d, UseShellExecute = true, Verb = "open" });
            }
            catch (Exception ex) { FileLogger.LogError("无法打开原始位置", ex); MyMessageBox.Show($"无法打开: {ex.Message}", "错误"); }
        }

        private void OpenFile(BackupLogEntry entry)
        {
            if (entry == null) return;
            if (File.Exists(entry.BackupPath))
                try { Process.Start(new ProcessStartInfo { FileName = entry.BackupPath, UseShellExecute = true }); }
                catch (Exception ex) { FileLogger.LogError("无法打开文件", ex); MyMessageBox.Show($"无法打开: {ex.Message}", "错误"); }
            else MyMessageBox.Show($"文件不存在: {entry.BackupPath}", "提示");
        }

        private void OpenFileLocation(BackupLogEntry entry)
        {
            if (entry == null) return;
            if (File.Exists(entry.BackupPath))
                try { Process.Start("explorer.exe", $"/select,\"{entry.BackupPath}\""); }
                catch (Exception ex) { FileLogger.LogError("无法打开位置", ex); MyMessageBox.Show($"无法打开: {ex.Message}", "错误"); }
            else MyMessageBox.Show($"文件不存在: {entry.BackupPath}", "提示");
        }

        private void OpenLogFolder()
        {
            OpenFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "文件备份监控助手", "logs"));
        }

        public string RecentLoadText => string.Format("加载最近 {0} 条日志", Math.Max(0, _settings?.RecentLogCount ?? 200));

        private void LoadRecentLogs()
        {
            try
            {
                var count = Math.Max(0, _settings?.RecentLogCount ?? 200);
                var recent = _logService.LoadRecent(count);
                Action updateAction = () => { Logs.Clear(); foreach (var e in recent) Logs.Add(e); };
                if (Application.Current != null) Application.Current.Dispatcher.Invoke(updateAction);
                else updateAction();
            }
            catch (Exception ex) { FileLogger.LogError("加载日志失败", ex); }
        }

        private void InitializeService()
        {
            if (_service != null)
                try { _service.Dispose(); } catch (Exception ex) { FileLogger.LogError("释放服务失败", ex); }

            _service = new BackupService(_settings);

            _service.OnBackup += (src, backup, size) =>
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    TotalBackups++; TotalSize += size;

                    // 用最长匹配找到正确的监控文件夹（和 BackupService.FindMatchingWatchFolder 一致）
                    string watchFolder = "";
                    int bestLen = 0;
                    string backupFolder = "";
                    foreach (var p in _settings.FolderPairs)
                    {
                        if (string.IsNullOrWhiteSpace(p.WatchFolder)) continue;
                        var full = Path.GetFullPath(p.WatchFolder);
                        if (!full.EndsWith(Path.DirectorySeparatorChar.ToString())) full += Path.DirectorySeparatorChar;
                        if (src.StartsWith(full, StringComparison.OrdinalIgnoreCase) && full.Length > bestLen)
                        { watchFolder = p.WatchFolder; backupFolder = p.BackupFolder; bestLen = full.Length; }
                    }

                    var entry = new BackupLogEntry
                    {
                        Time = DateTime.Now,
                        FileName = Path.GetFileName(src),
                        //RelativePath = GetRelativePath(watchFolder, src),
                        SourceFullPath = src,
                        BackupPath = backup,
                        //WatchFolder = src,
                        //BackupFolder = backup,
                        //WatchFolder = watchFolder,
                        //BackupFolder = backupFolder,
                        SizeBytes = size
                    };
                    try { _logService.Append(entry); } catch (Exception ex) { FileLogger.LogError("持久化日志失败", ex); }
                    if (UiVisible) { Logs.Insert(0, entry); while (Logs.Count > 500) Logs.RemoveAt(Logs.Count - 1); }
                });
            };

            _service.OnLog += msg =>
            {
                if (Application.Current != null)
                    Application.Current.Dispatcher.Invoke(() => Debug.WriteLine(msg));
            };
        }

        private static string GetRelativePath(string basePath, string path)
        {
            try
            {
                if (string.IsNullOrEmpty(basePath)) return Path.GetFileName(path);
                var baseFull = Path.GetFullPath(basePath);
                var targetFull = Path.GetFullPath(path);
                if (!baseFull.EndsWith(Path.DirectorySeparatorChar.ToString())) baseFull += Path.DirectorySeparatorChar;
                return Uri.UnescapeDataString(new Uri(baseFull).MakeRelativeUri(new Uri(targetFull)).ToString()).Replace('/', Path.DirectorySeparatorChar);
            }
            catch { try { return Path.GetFileName(path); } catch { return path; } }
        }

        public void Start()
        {
            try { _service.Start(); IsMonitoring = true; OnPropertyChanged("StatusText"); LoadRecentLogs(); }
            catch (Exception ex) { FileLogger.LogError("启动失败", ex); MyMessageBox.Show("启动失败: " + ex.Message, "错误"); }
        }

        public void Stop()
        {
            _service.Stop(); IsMonitoring = false; OnPropertyChanged("StatusText");
            try { _maintenanceTimer?.Dispose(); } catch (Exception ex) { FileLogger.LogError("停止定时器失败", ex); }
        }

        private void EnsureMaintenanceTimer()
        {
            try { _maintenanceTimer?.Dispose(); } catch { }
            _maintenanceTimer = new System.Threading.Timer(_ => _logService.PerformMaintenance(), null, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1));
        }

        private void OpenLogWindow()
        {
            if (_service == null)
                return;

            var win = new Views.LogWindow(_service);
            win.Owner = System.Windows.Application.Current?.MainWindow;
            win.Show();
        }

        private void OpenSettings()
        {
            bool wasRunning = IsMonitoring;
            if (wasRunning) Stop();

            var dlg = new Views.SettingsWindow(_settings);
            if (dlg.ShowDialog() == true)
            {
                _settings = dlg.Settings;
                SettingsService.Save(_settings);
                InitializeService();
                try { _maintenanceTimer?.Dispose(); } catch { }
                _logService = new Services.LogService(_settings);
                EnsureMaintenanceTimer();
                OnPropertyChanged("WatchFoldersText"); OnPropertyChanged("BackupFoldersText");
                OnPropertyChanged("WatchFolder"); OnPropertyChanged("BackupFolder");
                OnPropertyChanged("FolderPairDisplayList");
                OnPropertyChanged("StatusText"); OnPropertyChanged("RecentLoadText");
                if (wasRunning) Start();
            }
            else { if (wasRunning) Start(); }
        }

        private static void OpenFolder(string path) { if (Directory.Exists(path)) Process.Start("explorer.exe", path); }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return string.Format("{0:F1} KB", bytes / 1024.0);
            if (bytes < 1024L * 1024 * 1024) return string.Format("{0:F1} MB", bytes / (1024.0 * 1024));
            return string.Format("{0:F2} GB", bytes / (1024.0 * 1024 * 1024));
        }

        public void Save() => SettingsService.Save(_settings);

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));



        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            //try { _service?.Dispose(); } catch { }
            //try { _maintenanceTimer?.Dispose(); } catch { }            
            try { Stop(); } catch { }
        } 
    }
}