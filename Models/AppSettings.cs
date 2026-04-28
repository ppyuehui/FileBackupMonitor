using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FileBackupMonitor.Models
{
    /// <summary>
    /// 监控-备份文件夹对（一一对应）
    /// </summary>
    public class FolderPair
    {
        [JsonPropertyName("watchFolder")]
        public string WatchFolder { get; set; } = string.Empty;

        [JsonPropertyName("backupFolder")]
        public string BackupFolder { get; set; } = string.Empty;
    }

    public class AppSettings
    {
        /// <summary>监控-备份文件夹对列表（一一对应）</summary>
        [JsonPropertyName("folderPairs")]
        public List<FolderPair> FolderPairs { get; set; } = new List<FolderPair>();

        // ═══ 兼容旧配置 ═══
        // 旧配置用 watchFolders + backupFolders 两个独立列表，新版用 FolderPairs
        // 读取旧配置时自动迁移

        [JsonPropertyName("watchFolders")]
        public List<string> WatchFoldersCompat
        {
            set
            {
                if (value != null && FolderPairs.Count == 0)
                {
                    var backups = BackupFoldersCompatCache ?? new List<string>();
                    for (int i = 0; i < value.Count; i++)
                    {
                        FolderPairs.Add(new FolderPair
                        {
                            WatchFolder = value[i],
                            BackupFolder = i < backups.Count ? backups[i] : string.Empty
                        });
                    }
                }
            }
        }

        private List<string> BackupFoldersCompatCache;

        [JsonPropertyName("backupFolders")]
        public List<string> BackupFoldersCompat
        {
            set
            {
                BackupFoldersCompatCache = value;
                // 如果 FolderPairs 已经从 watchFolders 填充了但 backupFolder 还是空的，补上
                if (FolderPairs.Count > 0)
                {
                    for (int i = 0; i < FolderPairs.Count && i < value.Count; i++)
                    {
                        if (string.IsNullOrEmpty(FolderPairs[i].BackupFolder))
                            FolderPairs[i].BackupFolder = value[i];
                    }
                }
            }
        }

        [JsonPropertyName("watchFolder")]
        public string WatchFolder
        {
            get => FolderPairs.Count > 0 ? FolderPairs[0].WatchFolder : string.Empty;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    if (FolderPairs.Count == 0)
                        FolderPairs.Add(new FolderPair { WatchFolder = value });
                    else
                        FolderPairs[0].WatchFolder = value;
                }
            }
        }

        [JsonPropertyName("backupFolder")]
        public string BackupFolder
        {
            get => FolderPairs.Count > 0 ? FolderPairs[0].BackupFolder : string.Empty;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    if (FolderPairs.Count == 0)
                        FolderPairs.Add(new FolderPair { BackupFolder = value });
                    else
                        FolderPairs[0].BackupFolder = value;
                }
            }
        }

        /// <summary>延迟备份秒数</summary>
        [JsonPropertyName("delaySeconds")]
        public int DelaySeconds { get; set; } = 5;

        /// <summary>旧备份保留天数，0 = 永不删除</summary>
        [JsonPropertyName("retentionDays")]
        public int RetentionDays { get; set; } = 10;

        /// <summary>是否开机自启</summary>
        [JsonPropertyName("startWithWindows")]
        public bool StartWithWindows { get; set; }

        /// <summary>最小化到托盘</summary>
        [JsonPropertyName("minimizeToTray")]
        public bool MinimizeToTray { get; set; } = true;

        /// <summary>是否监控子文件夹</summary>
        [JsonPropertyName("includeSubfolders")]
        public bool IncludeSubfolders { get; set; } = true;

        /// <summary>是否使用深色主题</summary>
        [JsonPropertyName("isDarkTheme")]
        public bool IsDarkTheme { get; set; } = true;

        /// <summary>忽略的文件名通配符列表</summary>
        [JsonPropertyName("ignorePatterns")]
        public List<string> IgnorePatterns { get; set; } = new List<string>
        {
            "*.temp", "*.tmp", "*.tgz", "*.his", "*.for", "*.appdf", "*.def",
            "*.sor", "*.sin", "*.scr", "*.rp", "*.prd", "*.nol", "*.nin",
            "*.jnl", "*.itd", "*.inx", "*.inm", "*.in", "*.ikc", "*.gen",
            "*.dyl", "*.dfm", "*.cod", "*.apprj", "*.co1", "*.co2", "*.edf",
            "*.nis", "*.ia1", "*.sum", "*.bks", "*.ia2", "*.ads", "*.dwl",
            "*.dwl2", "*.adv", "*.plf", "*.rp1", "*.rp2", "*.in0", "*.msh",
            "*.odi", "*.bk$", "*.bak", "*.def",
            "*.asd", "*.xlk", "*.wbk", "~$*",
        };

        /// <summary>忽略的文件夹路径列表（支持通配符）</summary>
        [JsonPropertyName("ignoredFolders")]
        public List<string> IgnoredFolders { get; set; } = new List<string>();

        /// <summary>从磁盘加载到内存用于显示的最近日志条数</summary>
        [JsonPropertyName("recentLogCount")]
        public int RecentLogCount { get; set; } = 200;

        /// <summary>日志文件轮转的最大文件大小（MB）</summary>
        [JsonPropertyName("logMaxSizeMB")]
        public int LogMaxSizeMB { get; set; } = 10;

        /// <summary>轮转归档文件的保留天数</summary>
        [JsonPropertyName("logRetentionDays")]
        public int LogRetentionDays { get; set; } = 10;

        /// <summary>最大备份日志条数</summary>
        public int MaxBackupLogs { get; set; } = 1000;
    }
}
