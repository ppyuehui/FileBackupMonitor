using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FileBackupMonitor.Models;

namespace FileBackupMonitor.Converters
{
    public class BoolToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "🟢 监控中" : "⏹️ 已停止";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoolToStartTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "停止监控" : "开始监控";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => !(bool)value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => !(bool)value;
    }

    public class StringNotEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => !string.IsNullOrWhiteSpace(value as string);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class ZeroToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Visibility.Collapsed;

            int count;
            if (value is int i)
                count = i;
            else if (!int.TryParse(value.ToString(), out count))
                return Visibility.Collapsed;

            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}


    /// <summary>
    /// 日志筛选类型转换器：将 LogType 转换为中文显示
    /// </summary>
    public class LogFilterConverter : IValueConverter
    {
        public static readonly LogFilterConverter Instance = new LogFilterConverter();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is LogType type)
            {
                switch (type)
                {
                    case LogType.All: return "全部";
                    case LogType.Backup: return "已备份";
                    case LogType.Rename: return "重命名";
                    case LogType.NewFile: return "新文件";
                    case LogType.Completed: return "备份完成";
                    case LogType.Detect: return "检测";
                    case LogType.Error: return "错误";
                    case LogType.Warning: return "警告";
                    case LogType.Stop: return "停止";
                    case LogType.Cleanup: return "清理";
                    default: return "未知";
                }
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }