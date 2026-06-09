using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using FileBackupMonitor.Services;
using FileBackupMonitor.ViewModels;

namespace FileBackupMonitor.Views
{
    /// <summary>
    /// 实时日志窗口（按需显示，打开时才收集日志）
    /// </summary>
    public partial class LogWindow : Window
    {
        private readonly LogViewModel _vm;

        public LogWindow(BackupService backupService)
        {
            InitializeComponent();
            
            // 创建 ViewModel 并传入 backupService（会自动开始订阅日志）
            _vm = new LogViewModel(backupService);
            DataContext = _vm;

            // 绑定自动滚动 CheckBox
            AutoScrollCheck.DataContext = _vm;
            AutoScrollCheck.SetBinding(System.Windows.Controls.CheckBox.IsCheckedProperty, 
                new System.Windows.Data.Binding("AutoScroll"));

            // 绑定条数显示
            CountText.DataContext = _vm;
            CountText.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
                new System.Windows.Data.Binding("CountText"));
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            if (sender is System.Windows.Controls.ComboBox combo && 
                combo.SelectedItem is System.Windows.Controls.ComboBoxItem item &&
                int.TryParse(item.Tag?.ToString(), out int tagValue))
            {
                _vm.FilterType = (FileBackupMonitor.Models.LogType)tagValue;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            // 窗口关闭时释放资源，取消事件订阅
            _vm?.Dispose();
        }
    }
}
