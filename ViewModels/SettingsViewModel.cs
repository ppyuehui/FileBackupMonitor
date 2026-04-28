using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using FileBackupMonitor.Services;
using Logging;
using Microsoft.Win32;
using MyMessagebox;
using FileBackupMonitor.Models;
using System.Collections.Generic;

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

    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly Models.AppSettings _original;
        private readonly LogService _logService;
        public Models.AppSettings Settings { get; }

        public string IgnorePatternsText { get; set; }
        public string IgnoredFoldersText { get; set; }

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

            SaveCommand = new RelayCommand(win =>
            {
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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
