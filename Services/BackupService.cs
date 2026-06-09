using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Logging;

namespace FileBackupMonitor.Services
{
    /// <summary>
    /// 核心监控+备份服务（支持多对监控-备份文件夹一一对应）
    /// </summary>
    public class BackupService : IDisposable
    {
        private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        private readonly Dictionary<FileSystemWatcher, string> _watcherBackupMap = new Dictionary<FileSystemWatcher, string>();
        private readonly Models.AppSettings _settings;
        private readonly HashSet<string> _pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();
        private Timer _cleanupTimer;

        // 跟踪每个文件的上次备份时间（防重复备份）
        private readonly Dictionary<string, DateTime> _lastBackupTimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        // 跟踪刚创建的文件（避免 Created + Changed 重复触发备份）
        private readonly Dictionary<string, DateTime> _recentlyCreated = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>备份完成事件 (filePath, backupPath, size)</summary>
        public event Action<string, string, long> OnBackup;
        /// <summary>日志事件</summary>
        public event Action<string> OnLog;
        /// <summary>清理事件</summary>
        public event Action<string, int, long> OnCleanup;

        public BackupService(Models.AppSettings settings)
        {
            _settings = settings;
        }

        // ========== 文件忽略检查（原 IgnorePatterns）==========
        private bool IsFileIgnored(string path)
        {
            var fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName)) return false;

            // 临时文件快速检查
            if (fileName.EndsWith(".swp", StringComparison.OrdinalIgnoreCase)) return true;

            // 先检查文件夹路径忽略（优先级更高）
            if (IsFolderIgnored(path)) return true;

            // 再检查文件忽略模式
            var ext = Path.GetExtension(fileName);
            foreach (var pat in _settings?.IgnorePatterns ?? new List<string>())
            {
                try
                {
                    // 空模式跳过
                    if (string.IsNullOrWhiteSpace(pat)) continue;

                    // 情况1：忽略特定扩展名（如 .tmp, .log, .bak）
                    if (pat.StartsWith(".") && !pat.Contains("*") && !pat.Contains("?"))
                    {
                        if (string.Equals(pat, ext, StringComparison.OrdinalIgnoreCase))
                            return true;
                        continue;
                    }

                    // 情况2：忽略完整文件名（如 Thumbs.db, desktop.ini）
                    if (!pat.Contains("*") && !pat.Contains("?"))
                    {
                        if (string.Equals(pat, fileName, StringComparison.OrdinalIgnoreCase))
                            return true;
                        continue;
                    }

                    // 情况3：通配符匹配（如 *.tmp, test*.log）
                    if (pat == "*") return true;

                    var regexPattern = "^" + Regex.Escape(pat).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                    if (Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase))
                        return true;
                }
                catch (Exception ex)
                {
                    FileLogger.LogError($"检查文件名匹配失败: {pat}", ex);
                }
            }
            return false;
        }
        private bool IsFolderIgnored(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            foreach (var pattern in _settings?.IgnoredFolders ?? new List<string>())
            {
                try
                {
                    var normalizedPattern = pattern.Replace('/', '\\');
                    var normalizedPath = path.Replace('/', '\\');

                    // 拆分路径为段
                    var patternSegments = normalizedPattern.Split('\\');
                    var pathSegments = normalizedPath.Split('\\');

                    // 如果模式末尾是空字符串（以 \ 结尾），添加通配符段
                    if (patternSegments.Length > 0 && patternSegments[patternSegments.Length - 1] == "")
                        patternSegments = patternSegments.Concat(new[] { "*" }).ToArray();

                    // 尝试在 pathSegments 中找到匹配的起始位置
                    for (int start = 0; start <= pathSegments.Length - patternSegments.Length; start++)
                    {
                        bool match = true;
                        for (int j = 0; j < patternSegments.Length; j++)
                        {
                            var seg = patternSegments[j];
                            if (seg == "") continue;

                            var pathSeg = pathSegments[start + j];

                            if (seg == "*")
                            {
                                continue; // 匹配任意单级
                            }
                            else if (seg.Contains("*") || seg.Contains("?"))
                            {
                                var segRegex = "^" + Regex.Escape(seg).Replace("*", ".*").Replace("?", ".") + "$";
                                if (!Regex.IsMatch(pathSeg, segRegex, RegexOptions.IgnoreCase))
                                { match = false; break; }
                            }
                            else
                            {
                                if (!string.Equals(seg, pathSeg, StringComparison.OrdinalIgnoreCase))
                                { match = false; break; }
                            }
                        }
                        if (match) return true;
                    }
                }
                catch (Exception ex) { FileLogger.LogError($"检查文件夹匹配失败: {pattern}", ex); }
            }
            return false;
        }


        public bool IsRunning => _watchers.Count > 0;

        public void Start()
        {
            var pairs = _settings.FolderPairs ?? new List<Models.FolderPair>();
            if (pairs.Count == 0)
                throw new DirectoryNotFoundException("未设置监控文件夹");

            foreach (var pair in pairs)
            {
                if (string.IsNullOrWhiteSpace(pair.WatchFolder) || !Directory.Exists(pair.WatchFolder))
                    continue;

                var watcher = new FileSystemWatcher(pair.WatchFolder)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime,
                    IncludeSubdirectories = _settings.IncludeSubfolders,
                    EnableRaisingEvents = true,
                    Filter = "*.*"
                };
                _watcherBackupMap[watcher] = pair.BackupFolder;
                watcher.Changed += OnFileChanged;
                watcher.Created += OnFileCreated;
                watcher.Renamed += OnFileRenamed;
                _watchers.Add(watcher);

                // 确保备份目录存在
                if (!string.IsNullOrWhiteSpace(pair.BackupFolder))
                    try { Directory.CreateDirectory(pair.BackupFolder); } catch { }
            }

            if (_watchers.Count == 0)
                throw new DirectoryNotFoundException("所有监控文件夹都不存在");

            _cleanupTimer = new Timer(_ => CleanupOldBackups(), null,
                TimeSpan.FromMinutes(1), TimeSpan.FromHours(1));

            var names = string.Join(" | ", pairs.Where(p => Directory.Exists(p.WatchFolder))
                .Select(p => Path.GetFileName(p.WatchFolder) + " → " + Path.GetFileName(p.BackupFolder)));
            OnLog?.Invoke("✅ 开始监控: " + names);
        }

        public void Stop()
        {
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= OnFileChanged;
                watcher.Created -= OnFileCreated;
                watcher.Renamed -= OnFileRenamed;
                watcher.Dispose();
            }
            _watchers.Clear();
            _watcherBackupMap.Clear();

            if (_cleanupTimer != null) { _cleanupTimer.Dispose(); _cleanupTimer = null; }

            lock (_lock) { _lastBackupTimes.Clear(); _pending.Clear(); _recentlyCreated.Clear(); }

            OnLog?.Invoke("⏹️ 监控已停止");
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(e.FullPath) || !File.Exists(e.FullPath) || Directory.Exists(e.FullPath)) return;
                if (IsTemporaryFile(e.FullPath)) return;

                // 跳过刚创建的文件（复制/新建），只备份真正修改过的文件
                lock (_lock)
                {
                    if (_recentlyCreated.TryGetValue(e.FullPath, out var createTime))
                    {
                        if ((DateTime.Now - createTime).TotalSeconds < _settings.DelaySeconds)
                            return;
                        _recentlyCreated.Remove(e.FullPath);
                    }
                }

                var watcher = sender as FileSystemWatcher;
                var backupFolder = watcher != null && _watcherBackupMap.TryGetValue(watcher, out var bf) ? bf : string.Empty;

                if (IsOfficeFile(e.FullPath))
                    ScheduleBackup(e.FullPath, backupFolder, "Changed(Office兜底)", delayOverrideMs: 5000);
                else
                    ScheduleBackup(e.FullPath, backupFolder, "Changed");
            }
            catch (Exception ex)
            {
                FileLogger.LogError("OnFileChanged 异常", ex);
                OnLog?.Invoke($"❌ 处理变更事件失败: {e?.FullPath} - {ex.Message}");
            }
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            if (string.IsNullOrEmpty(e.FullPath) || !File.Exists(e.FullPath) || Directory.Exists(e.FullPath)) return;
            if (IsTemporaryFile(e.FullPath)) return;
            lock (_lock) { _recentlyCreated[e.FullPath] = DateTime.Now; }
            OnLog?.Invoke($"📝 检测到新文件: {e.FullPath}");
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.FullPath) || Directory.Exists(e.FullPath)) return;
            var fileName = Path.GetFileName(e.FullPath);
            if (fileName != null && fileName.StartsWith("~$", StringComparison.OrdinalIgnoreCase)) return;
            if (IsTemporaryFile(e.FullPath)) return;

            var watcher = sender as FileSystemWatcher;
            var backupFolder = watcher != null && _watcherBackupMap.TryGetValue(watcher, out var bf) ? bf : string.Empty;

            OnLog?.Invoke($"🔄 检测到重命名: {e.OldFullPath} → {e.FullPath}");

            // 只有 Office 文件的重命名是保存流程的一部分（临时文件重命名为原文件名），触发备份
            // 非 Office 文件的重命名只是改名，不是修改内容，不触发备份
            if (IsOfficeFile(e.FullPath))
                ScheduleBackup(e.FullPath, backupFolder, "Renamed(Office保存完成)", delayOverrideMs: 1000);
        }

        private void ScheduleBackup(string fullPath, string backupFolder, string eventType, int? delayOverrideMs = null)
        {
            if (!File.Exists(fullPath)) return;

            lock (_lock)
            {
                if (_pending.Contains(fullPath)) return;
                if (_lastBackupTimes.TryGetValue(fullPath, out var lastTime))
                {
                    if ((DateTime.Now - lastTime).TotalSeconds < 1) return;
                }
                _pending.Add(fullPath);
            }

            OnLog?.Invoke($"🔍 {eventType}: {fullPath}");
            var delayMs = delayOverrideMs ?? (Math.Max(0, _settings.DelaySeconds) * 1000);

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delayMs);
                    if (!File.Exists(fullPath)) { OnLog?.Invoke($"⚠️ 文件不存在: {fullPath}"); return; }
                    await BackupFileAsync(fullPath, backupFolder);
                }
                catch (Exception ex)
                {
                    FileLogger.LogError("备份异常", ex);
                    OnLog?.Invoke($"❌ 备份失败: {fullPath} - {ex.Message}");
                }
                finally { lock (_lock) { _pending.Remove(fullPath); } }
            });
        }

        private bool IsTemporaryFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(fileName)) return false;
            if (fileName.StartsWith("~$") || fileName.EndsWith(".tmp") || fileName.EndsWith(".bak")
                || fileName.EndsWith(".swp") || fileName.EndsWith("~")) return true;
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (ext == ".asd" || ext == ".xlk" || ext == ".wbk" || ext == ".tmp") return true;
            return false;
        }

        private bool IsOfficeFile(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext == ".doc" || ext == ".docx" || ext == ".xls" || ext == ".xlsx" || ext == ".ppt" || ext == ".pptx";
        }

        private bool IsFileLocked(string filePath)
        {
            try { using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { } return false; }
            catch (IOException) { return true; }
        }

        private async Task BackupFileAsync(string sourcePath, string backupFolder)
        {
            if (!File.Exists(sourcePath) || IsFileIgnored(sourcePath) || IsTemporaryFile(sourcePath)) return;
            var fileName = Path.GetFileName(sourcePath);
            if (fileName != null && fileName.StartsWith("~$", StringComparison.OrdinalIgnoreCase)) return;
            if (string.IsNullOrWhiteSpace(backupFolder) || !Directory.Exists(backupFolder)) return;
            if (sourcePath.StartsWith(backupFolder, StringComparison.OrdinalIgnoreCase)) return;

            // 找到源文件所属的监控文件夹
            string watchFolder = FindMatchingWatchFolder(sourcePath);

            // 等待文件可访问
            bool canAccess = false;
            for (int i = 0; i < 30; i++)
            {
                if (!File.Exists(sourcePath)) return;
                if (!IsFileLocked(sourcePath))
                {
                    try { using (var fs = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { } canAccess = true; break; }
                    catch (IOException) { }
                }
                await Task.Delay(1000);
            }
            if (!canAccess) { OnLog?.Invoke($"⚠️ 文件占用，放弃: {sourcePath}"); return; }

            await PerformBackupAsync(sourcePath, watchFolder, backupFolder);
            lock (_lock) { _lastBackupTimes[sourcePath] = DateTime.Now; }
        }

        private string FindMatchingWatchFolder(string filePath)
        {
            string best = null;
            int bestLen = 0;
            foreach (var pair in _settings.FolderPairs ?? new List<Models.FolderPair>())
            {
                if (string.IsNullOrWhiteSpace(pair.WatchFolder)) continue;
                var full = Path.GetFullPath(pair.WatchFolder);
                if (!full.EndsWith(Path.DirectorySeparatorChar.ToString())) full += Path.DirectorySeparatorChar;
                if (filePath.StartsWith(full, StringComparison.OrdinalIgnoreCase) && full.Length > bestLen)
                { best = pair.WatchFolder; bestLen = full.Length; }
            }
            return best ?? _settings.FolderPairs?.FirstOrDefault()?.WatchFolder ?? string.Empty;
        }

        private async Task PerformBackupAsync(string sourcePath, string watchFolder, string backupFolder)
        {
            try
            {
                var fi = new FileInfo(sourcePath);
                if (!fi.Exists) return;

                var relativePath = string.Empty;
                if (!string.IsNullOrEmpty(watchFolder) && sourcePath.StartsWith(watchFolder, StringComparison.OrdinalIgnoreCase))
                    relativePath = GetRelativePath(watchFolder, sourcePath);
                else
                    relativePath = fi.Name;

                var backupDir = Path.Combine(backupFolder, Path.GetDirectoryName(relativePath) ?? string.Empty);
                Directory.CreateDirectory(backupDir);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var nameWithoutExt = Path.GetFileNameWithoutExtension(fi.Name);
                var ext = fi.Extension;
                var backupName = nameWithoutExt + "_" + timestamp + ext;
                var backupPath = Path.Combine(backupDir, backupName);

                int counter = 1;
                while (File.Exists(backupPath))
                {
                    backupName = nameWithoutExt + "_" + timestamp + "_" + counter + ext;
                    backupPath = Path.Combine(backupDir, backupName);
                    counter++;
                }

                await Task.Run(() => File.Copy(sourcePath, backupPath, overwrite: false));
                var size = new FileInfo(backupPath).Length;

                OnLog?.Invoke("📦 已备份: " + relativePath + " → " + backupName);
                OnBackup?.Invoke(sourcePath, backupPath, size);
            }
            catch (Exception ex)
            {
                FileLogger.LogError($"⚠️ 备份失败: {sourcePath}", ex);
                OnLog?.Invoke($"⚠️ 备份失败: {sourcePath} - {ex.Message}");
            }
        }

        private static string GetRelativePath(string basePath, string path)
        {
            try
            {
                var baseFull = Path.GetFullPath(basePath);
                var targetFull = Path.GetFullPath(path);
                if (!baseFull.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                    baseFull += Path.DirectorySeparatorChar;
                var relativeUri = new Uri(baseFull).MakeRelativeUri(new Uri(targetFull));
                return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
            }
            catch (Exception ex)
            {
                FileLogger.LogError("计算相对路径失败", ex);
                try { return Path.GetFileName(path); } catch { return path; }
            }
        }

        private static DateTime GetBackupDateFromName(FileInfo fi)
        {
            var name = Path.GetFileNameWithoutExtension(fi.Name);
            var match = Regex.Match(name, @"_(\d{8})_\d{6}$");
            if (match.Success && DateTime.TryParseExact(match.Groups[1].Value, "yyyyMMdd",
                null, System.Globalization.DateTimeStyles.None, out var dt))
                return dt;
            return fi.CreationTime;
        }

        public void CleanupOldBackups()
        {
            if (_settings.RetentionDays <= 0) return;
            foreach (var pair in _settings.FolderPairs ?? new List<Models.FolderPair>())
            {
                var backupFolder = pair.BackupFolder;
                if (string.IsNullOrWhiteSpace(backupFolder) || !Directory.Exists(backupFolder)) continue;
                try
                {
                    var cutoff = DateTime.Now.AddDays(-_settings.RetentionDays);
                    var files = Directory.GetFiles(backupFolder, "*", SearchOption.AllDirectories);
                    int deleted = 0;
                    long deletedSize = 0;
                    foreach (var file in files)
                    {
                        var fi = new FileInfo(file);
                        var fileDate = GetBackupDateFromName(fi);
                        if (fileDate < cutoff) { try { if (fi.IsReadOnly) fi.Attributes = FileAttributes.Normal; deletedSize += fi.Length; fi.Delete(); deleted++; } catch (Exception ex) { FileLogger.LogError("删除过期备份失败: " + file, ex); } }
                    }
                    CleanEmptyDirectories(backupFolder);
                    if (deleted > 0)
                    {
                        OnCleanup?.Invoke(backupFolder, deleted, deletedSize);
                        FileLogger.LogInfo($"清理过期备份: {backupFolder}, 删除了 {deleted} 个文件");
                        OnLog?.Invoke($"🧹 清理 {deleted} 个过期备份: {backupFolder}");
                    }
                }
                catch (Exception ex) { FileLogger.LogError("清理失败: " + backupFolder, ex); }
            }
        }

        private static void CleanEmptyDirectories(string path)
        {
            foreach (var dir in Directory.GetDirectories(path))
            {
                CleanEmptyDirectories(dir);
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    try { Directory.Delete(dir); } catch { }
            }
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
