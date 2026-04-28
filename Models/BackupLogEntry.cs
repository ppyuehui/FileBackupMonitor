using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace FileBackupMonitor.Models
{
    public class BackupLogEntry : INotifyPropertyChanged
    {
        public DateTime Time { get; set; }
        public string FileName { get; set; } = string.Empty;
        //public string RelativePath { get; set; } = string.Empty;
        public string SourceFullPath { get; set; } = string.Empty;
        public string BackupPath { get; set; } = string.Empty;
        //public string WatchFolder { get; set; } = string.Empty;
        //public string BackupFolder { get; set; } = string.Empty;
        public long SizeBytes { get; set; }

        public string SizeText
        {
            get
            {
                if (SizeBytes < 1024) return SizeBytes + " B";
                if (SizeBytes < 1024 * 1024) return string.Format("{0:F1} KB", SizeBytes / 1024.0);
                return string.Format("{0:F1} MB", SizeBytes / (1024.0 * 1024));
            }
        }

        ///// <summary>监控文件夹名称（简短显示）</summary>
        //public string WatchFolderName =>
        //    string.IsNullOrEmpty(WatchFolder) ? "" : (Path.GetFileName(WatchFolder) ?? WatchFolder);

        ///// <summary>备份文件夹名称（简短显示）</summary>
        //public string BackupFolderName =>
        //    string.IsNullOrEmpty(BackupFolder) ? "" : (Path.GetFileName(BackupFolder) ?? BackupFolder);

        ///// <summary>来源/目标简要描述</summary>
        //public string FolderPairText =>
        //    $"{WatchFolderName} → {BackupFolderName}";

        private string _status = "成功";
        public string Status
        {
            get { return _status; }
            set { _status = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
