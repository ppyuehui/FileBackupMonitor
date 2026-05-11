using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FileBackupMonitor.Models;
using FileBackupMonitor.Services;
using FileBackupMonitor.ViewModels;

namespace FileBackupMonitor.ViewModels
{
    /// <summary>
    /// 实时日志窗口的 ViewModel（按需显示）
    /// </summary>
    public class LogViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly BackupService _backupService;
        private bool _autoScroll = true;

        public ObservableCollection<Models.LogEntry> LiveLogs { get; } = new ObservableCollection<Models.LogEntry>();

        public bool AutoScroll
        {
            get => _autoScroll;
            set { _autoScroll = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 日志条数（计数属性，用于绑定）
        /// </summary>
        public int LogCount => LiveLogs.Count;

        /// <summary>
        /// 显示文本：条数: N
        /// </summary>
        public string CountText => $"条数: {FilteredLogs.Count}";

        public ICommand ClearLogsCommand { get; }

        /// <summary>
        /// 当前筛选类型
        /// </summary>
        public LogType FilterType
        {
            get => _filterType;
            set
            {
                if (_filterType != value)
                {
                    _filterType = value;
                    OnPropertyChanged();
                    ApplyFilter();
                }
            }
        }

        /// <summary>
        /// 筛选后的日志（UI 绑定源）
        /// </summary>
        public ObservableCollection<LogEntry> FilteredLogs { get; } = new ObservableCollection<LogEntry>();

        private LogType _filterType = LogType.All;


        public LogViewModel(BackupService backupService)
        {
            _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));

            // 订阅实时日志事件
            _backupService.OnLog += OnLogReceived;
            // 初始化时应用一次筛选（显示全部）
            ApplyFilter();

            // 监听集合变化，每当日志增删时更新计数显示
            LiveLogs.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(LogCount));
                //ApplyFilter();  // 重新筛选并更新 CountText
            };

            ClearLogsCommand = new RelayCommand(_ => ClearLogs());
        }

        /// <summary>
        /// 收到日志消息（来自 BackupService.OnLog）
        /// </summary>
        /// <summary>
        /// 根据 FilterType 筛选日志并更新 FilteredLogs
        /// </summary>
        private void ApplyFilter()
        {
            FilteredLogs.Clear();
            foreach (var log in LiveLogs)
            {
                if (ShouldInclude(log))
                    FilteredLogs.Add(log);
            }
            // 更新计数文本
            OnPropertyChanged(nameof(CountText));
        }

        private LogType GetLogType(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return LogType.All;

            // 使用 StartsWith 判断 emoji 前缀（避免多字节字符问题）
            if (msg.StartsWith("✅")) return LogType.Backup;
            if (msg.StartsWith("🔄")) return LogType.Rename;
            if (msg.StartsWith("📝")) return LogType.NewFile;
            if (msg.StartsWith("📦")) return LogType.Completed;
            if (msg.StartsWith("🔍")) return LogType.Detect;
            if (msg.StartsWith("❌")) return LogType.Error;
            if (msg.StartsWith("⚠️")) return LogType.Warning;
            if (msg.StartsWith("⏹️")) return LogType.Stop;
            if (msg.StartsWith("🧹")) return LogType.Cleanup;
            return LogType.All;
        }

        /// <summary>
        /// 判断日志是否应包含在当前筛选中
        /// </summary>
        private bool ShouldInclude(LogEntry log)
        {
            if (FilterType == LogType.All) return true;
            return log.Type == FilterType;
        }

        private void OnLogReceived(string message)
        {
            // 确保在 UI 线程更新（BackupService 可能在后台线程触发）
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var entry = new Models.LogEntry
                {
                    TimeStamp = DateTime.Now.ToString("HH:mm:ss"),
                    Message = message,
                    Color = GetColorByIcon(message),
                    Type = GetLogType(message)  // 自动识别类型
                };

                LiveLogs.Insert(0, entry);  // 最新日志在顶部

                // 限制最大数量，避免内存无限增长
                while (LiveLogs.Count > 2000)
                    LiveLogs.RemoveAt(LiveLogs.Count - 1); // 移除最旧的

                // 实时筛选：如果符合当前筛选条件，立即添加到 FilteredLogs
                if (ShouldInclude(entry))
                    FilteredLogs.Insert(0, entry);

                // 全选模式下保持同步
                if (FilterType == LogType.All)
                {
                    while (FilteredLogs.Count > LiveLogs.Count)
                        FilteredLogs.RemoveAt(FilteredLogs.Count - 1);
                }
            });
        }

        /// <summary>
        /// 根据消息前缀图标确定背景颜色（柔和浅色）
        /// </summary>
        private Color GetColorByIcon(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return Color.FromRgb(245, 245, 245);

            // 使用 StartsWith 判断前缀（支持 emoji 多字节字符）
            if (msg.StartsWith("✅")) return Color.FromRgb(163, 238, 163);   // 已备份：鲜绿 #A3EEA3
            if (msg.StartsWith("🔄")) return Color.FromRgb(102, 204, 255);   // 重命名：鲜蓝 #66CCFF
            if (msg.StartsWith("📝")) return Color.FromRgb(255, 234, 167);   // 新文件：鲜黄 #FFEAA7
            if (msg.StartsWith("📦")) return Color.FromRgb(163, 238, 163);   // 备份完成：鲜绿 #A3EEA3
            if (msg.StartsWith("🔍")) return Color.FromRgb(102, 224, 255);   // 检测：鲜蓝 #66E0FF
            if (msg.StartsWith("❌")) return Color.FromRgb(255, 128, 128);   // 错误：鲜红 #FF8080
            if (msg.StartsWith("⚠️")) return Color.FromRgb(255, 200, 150);   // 警告：鲜橙 #FFC896
            if (msg.StartsWith("⏹️")) return Color.FromRgb(200, 200, 200);   // 停止：鲜灰 #C8C8C8
            if (msg.StartsWith("🧹")) return Color.FromRgb(255, 234, 190);   // 清理：鲜杏 #FFEABE

            return Color.FromRgb(230, 230, 230); // 默认：浅灰
        }

        /// <summary>
        /// 清空日志
        /// </summary>
        private void ClearLogs()
        {
            LiveLogs.Clear();
            FilteredLogs.Clear();
            OnPropertyChanged(nameof(LogCount));
            OnPropertyChanged(nameof(CountText));
        }

        /// <summary>
        /// 释放资源（取消事件订阅）
        /// </summary>
        public void Dispose()
        {
            if (_backupService != null)
                _backupService.OnLog -= OnLogReceived;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); 
    }
}
