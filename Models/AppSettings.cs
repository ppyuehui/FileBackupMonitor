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
            "*.dll", "*.exe", "*.pdb", "*.pyc", "*.class", "*.o", "*.obj",
            "Thumbs.db", ".DS_Store",
        };

        /// <summary>忽略的文件夹路径列表（支持通配符）</summary>
        [JsonPropertyName("ignoredFolders")]
        public List<string> IgnoredFolders { get; set; } = new List<string>
        {
            ".vs", ".git", "node_modules", "__pycache__",
            "obj", ".idea", ".svn", "bin"
        };

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
