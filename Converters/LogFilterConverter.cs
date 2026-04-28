using System;
using System.Globalization;
using System.Windows.Data;

namespace FileBackupMonitor.Converters
{
    public sealed class LogFilterConverter : IValueConverter
    {
        // x:Static local:LogFilterConverter.Instance 在 XAML 中引用此静态属性
        public static LogFilterConverter Instance { get; } = new LogFilterConverter();

        private LogFilterConverter() { }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "全部";

            // 支持 int 或可解析为 int 的字符串
            if (!int.TryParse(value.ToString(), out int v)) return value.ToString();

            // 使用传统 switch 语句（C# 7.3 兼容）
            switch (v)
            {
                case 0:
                    return "全部";
                case 1:
                    return "📦 已备份";
                case 2:
                    return "🔄 重命名";
                case 3:
                    return "📝 新文件";
                case 4:
                    return "📦 备份完成";
                case 5:
                    return "🔍 检测";
                case 6:
                    return "❌ 错误";
                case 7:
                    return "⚠️ 警告";
                case 8:
                    return "⏹️ 停止";
                case 9:
                    return "🧹 清理";
                default:
                    return $"类型 {v}";
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}