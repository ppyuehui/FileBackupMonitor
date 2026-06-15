using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using FileBackupMonitor.Models;
using Logging;

namespace FileBackupMonitor.Services
{
    /// <summary>
    /// 简单的日志持久化服务，按行保存 JSON（jsonl），用于在后台运行时仍然保留日志并降低内存占用
    /// </summary>
    public class LogService
    {
        // 日志目录路径
        private readonly string _logDir;
        // 日志文件路径
        private readonly string _logFile;
        // 用于线程同步的锁对象
        private readonly object _lock = new object();
        // 应用程序设置
        private readonly Models.AppSettings _settings;

        /// <summary>
        /// LogService 构造函数，初始化日志服务和相关路径
        /// </summary>
        /// <param name="settings">应用程序设置，如果为null则使用默认设置</param>
        public LogService(Models.AppSettings settings = null)
        {
            _settings = settings ?? new Models.AppSettings();
            // 在应用程序数据目录中创建日志目录路径
            _logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "文件备份监控助手", "logs");
            // 组合完整的日志文件路径
            _logFile = Path.Combine(_logDir, "logs.jsonl");
            try { Directory.CreateDirectory(_logDir); } catch (Exception ex) { FileLogger.LogError("创建日志目录", ex); }
        }

        /// <summary>
        /// 对外暴露的维护入口：执行轮转与清理
        /// </summary>
        public void PerformMaintenance()
        {
            try { TrimIfNeeded(); } catch (Exception ex) { FileLogger.LogError("维护日志", ex); }
        }

        /// <summary>
        /// 添加新的备份日志条目到日志文件
        /// </summary>
        /// <param name="entry">要添加的备份日志条目</param>
        public void Append(BackupLogEntry entry)
        {
            try
            {
                // 在写入前检查是否需要裁剪
                try { TrimIfNeeded(); } catch (Exception ex) { FileLogger.LogError("在写入前检查是否需要裁剪", ex); }

                // 将日志条目序列化为JSON格式
                var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                // 使用锁确保线程安全
                lock (_lock)
                {
                    // 以追加模式打开文件流
                    using (var fs = new FileStream(_logFile, FileMode.Append, FileAccess.Write, FileShare.Read))
                    using (var sw = new StreamWriter(fs))
                    {
                        // 写入JSON行
                        sw.WriteLine(json);
                    }
                }
            }
            catch(Exception ex) {FileLogger.LogError("写入日志", ex); }
        }

        /// <summary>
        /// 检查并裁剪日志文件（当日志条目数超过限制时）
        /// </summary>
        private void TrimIfNeeded()
        {
            try
            {
                // 如果日志文件不存在，直接返回
                if (!File.Exists(_logFile)) return;
                
                // 使用配置中的备份日志数量
                var maxBackupLogs = _settings?.MaxBackupLogs ?? 100;
                
                // 使用队列只保留最后maxBackupLogs行，避免读取整个文件
                var queue = new Queue<string>();
                using (var fs = new FileStream(_logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        queue.Enqueue(line);
                        if (queue.Count > maxBackupLogs)
                            queue.Dequeue();
                    }
                }
                
                // 如果行数超过限制，重写文件
                if (queue.Count > 0)
                {
                    var linesToKeep = queue.ToArray();
                    File.WriteAllLines(_logFile, linesToKeep);
                }
            }
            catch (Exception ex) { FileLogger.LogError("裁剪日志文件", ex); } 
        }

        /// <summary>
        /// 获取最近的文件备份日志
        /// </summary>
        /// <returns>最近的备份日志列表</returns>
        public List<BackupLogEntry> GetRecentBackupLogs()
        {
            var count = _settings?.MaxBackupLogs ?? 100;
            return LoadRecent(count);
        }


        /// <summary>
        /// 加载最近的日志条目
        /// </summary>
        /// <param name="max">要加载的最大条目数</param>
        /// <returns>最近的日志条目列表</returns>
        public List<BackupLogEntry> LoadRecent(int max)
        {
            var list = new List<BackupLogEntry>();
            try
            {
                if (!File.Exists(_logFile)) return list;

                var queue = new Queue<string>();
                using (var fs = new FileStream(_logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        queue.Enqueue(line);
                        if (queue.Count > max)
                            queue.Dequeue();
                    }
                }

                var lines = queue.ToArray();
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    try
                    {
                        var e = JsonSerializer.Deserialize<BackupLogEntry>(lines[i],
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (e != null) list.Add(e);
                    }
                    catch (Exception ex) { FileLogger.LogError("加载日志条目", ex); }
                }
            }
            catch (Exception ex) { FileLogger.LogError("加载日志", ex); }
            return list;
        }

        /// <summary>
        /// 获取日志摘要信息（总条目数和总大小）
        /// </summary>
        /// <returns>包含日志条目数和总大小的元组</returns>
        public (int Count, long TotalSize) GetSummary()
        {
            int count = 0;
            long size = 0;
            try
            {
                // 如果日志文件不存在，返回(0,0)
                if (!File.Exists(_logFile)) return (0, 0);
                
                // 逐行读取日志文件
                foreach (var line in File.ReadLines(_logFile))
                {
                    try
                    {
                        var e = JsonSerializer.Deserialize<BackupLogEntry>(line, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (e != null)
                        {
                            count++;
                            size += e.SizeBytes;
                        }
                    }
                    catch (Exception ex) { FileLogger.LogError("获取日志摘要", ex); } 
                }
            }
            catch (Exception ex) { FileLogger.LogError("获取日志摘要", ex); }
            return (count, size);
        }


        /// <summary>
        /// Scan all backup folders on disk to get real file count and total size
        /// </summary>
        public (int Count, long TotalSize) ScanBackupFolders(List<FolderPair> folderPairs)
        {
            int count = 0;
            long size = 0;
            if (folderPairs == null) return (0, 0);
            foreach (var pair in folderPairs)
            {
                var backupFolder = pair.BackupFolder;
                if (string.IsNullOrWhiteSpace(backupFolder) || !Directory.Exists(backupFolder)) continue;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(backupFolder, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var fi = new FileInfo(file);
                            count++;
                            size += fi.Length;
                        }
                        catch (Exception ex) { FileLogger.LogError("获取文件信息失败: " + file, ex); }
                    }
                }
                catch (Exception ex) { FileLogger.LogError("Scan backup folder failed: " + backupFolder, ex); }
            }
            return (count, size);
        }

        public void Clear()
        {
            try
            {
                // 如果日志文件存在
                if (File.Exists(_logFile))
                {
                    // 使用锁确保线程安全
                    lock (_lock)
                    {
                        // 清空文件内容
                        File.WriteAllText(_logFile, string.Empty);
                    }
                }
            }
            catch (Exception ex) { FileLogger.LogError("清空日志", ex); }
        }
    }
}


