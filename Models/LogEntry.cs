using System;
using System.Windows.Media;

namespace FileBackupMonitor.Models
{
    /// <summary>
    /// 日志条目（用于 UI 显示）
    /// </summary>
    public class LogEntry
    {
        public string TimeStamp { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Color Color { get; set; } = Colors.Black;
        public Brush ColorBrush => new SolidColorBrush(Color);
        
        /// <summary>
        /// 日志类型（用于筛选）
        /// </summary>
        public LogType Type { get; set; }
    }

    /// <summary>
    /// 日志类型枚举
    /// </summary>
    public enum LogType
    {
        All = 0,        // 全部（显示所有）
        Backup = 1,     // 已备份 ✅
        Rename = 2,     // 重命名 🔄
        NewFile = 3,    // 新文件 📝
        Completed = 4,  // 备份完成 📦
        Detect = 5,     // 检测 🔍
        Error = 6,      // 错误 ❌
        Warning = 7,    // 警告 ⚠️
        Stop = 8,       // 停止 ⏹️
        Cleanup = 9     // 清理 🧹
    }
}