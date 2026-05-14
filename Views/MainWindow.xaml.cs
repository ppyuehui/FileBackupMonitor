using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using AutoUpdater;
using Logging;
using MyMessagebox;
using static MyMessagebox.MyMessageBox;

namespace FileBackupMonitor.Views
{
    public partial class MainWindow : Window
    {
        private NotifyIcon _notifyIcon;
        private bool _exitRequestedFromTray = false;
        private readonly UpdateManager _updateManager = new UpdateManager();
        private UpdateInfo _pendingUpdate;

        public MainWindow()
        {
            FileLogger.Initialize(logSubDir: "文件备份监控助手", maxDaysToKeep: 7);

            InitializeComponent();
            this.DataContext = new ViewModels.MainViewModel();
            InitTrayIcon();
            this.Closing += MainWindow_Closing;

            CheckForUpdateAsync();
        }

        private void InitTrayIcon()
        {
            _notifyIcon = new NotifyIcon();

            Icon icon = null;           
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                var names = asm.GetManifestResourceNames();
                var icoName = names.FirstOrDefault(n => n.EndsWith(".ico", StringComparison.OrdinalIgnoreCase));
                if (icoName != null)
                {
                    using (var rs = asm.GetManifestResourceStream(icoName))
                    {
                        if (rs != null)
                            icon = new Icon(rs);
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogError("加载托盘图标失败（嵌入资源）: ", ex);
            }

            // 最后回退到系统默认图标，确保不会因找不到图标而抛出
            _notifyIcon.Icon = icon ?? SystemIcons.Application;
            _notifyIcon.Visible = false;
            _notifyIcon.Text = "文件备份监控助手";
            // 创建右键菜单
            var menu = new ContextMenuStrip();

            menu.Items.Add("打开", null, (s, e) => ShowWindowFromTray());
            menu.Items.Add("退出", null, (s, e) => ExitFromTray());
            // 设置菜单
            _notifyIcon.ContextMenuStrip = menu;
            // 添加双击事件
            _notifyIcon.DoubleClick += (s, e) => ShowWindowFromTray();
        }

        private void ShowWindowFromTray()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            _notifyIcon.Visible = false;

            // 通知 ViewModel 界面已显示，恢复 UI 相关的日志收集
            var vmShow = this.DataContext as ViewModels.MainViewModel;
            if (vmShow != null)
                vmShow.UiVisible = true;
        }

        private void ExitFromTray()
        {
            var vm = this.DataContext as ViewModels.MainViewModel;
            try 
            { 
                vm?.Stop(); 
                vm?.Dispose();
            } 
            catch (Exception ex) { FileLogger.LogError("停止监控失败: ", ex); }

            // 标记为托盘发起的退出，随后触发正常的应用退出流程
            _exitRequestedFromTray = true;

            // 不在这里 Dispose _notifyIcon，留给 Closing 统一处理
            System.Windows.Application.Current.Shutdown();
        }      

        // ========== 自动检查更新（每天一次）==========
        private async void CheckForUpdateAsync()
        {
            try
            {
                var updateInfo = await _updateManager.CheckForUpdateAsync();
                Dispatcher.Invoke(() =>
                {
                    if (updateInfo != null)
                    {
                        _pendingUpdate = updateInfo;
                        btnUpdate.Visibility = Visibility.Visible;
                        btnUpdate.ToolTip = $"当前版本: {updateInfo.CurrentVersion}\n" +
                                           $"最新版本: {updateInfo.LatestVersion}\n" +
                                           $"文件大小: {updateInfo.FileSizeText}\n\n" +
                                           $"更新说明:\n{updateInfo.ReleaseNotes}";
                    }
                    else
                    {
                        btnCheckUpdate.Content = "没有更新";
                        btnCheckUpdate.IsEnabled = false;
                        Task.Delay(3000).ContinueWith(_ => Dispatcher.Invoke(() =>
                        {
                            btnCheckUpdate.Content = "🔍 检查更新";
                            btnCheckUpdate.IsEnabled = true;
                        }));
                    }
                });
            }
            catch (Exception ex)
            {
                FileLogger.LogError("检查更新失败", ex);
            }
        }

        // ========== 手动检查更新 ==========
        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnCheckUpdate.IsEnabled = false;
                btnCheckUpdate.Content = "⏳ 检查中...";

                var updateInfo = await _updateManager.CheckForUpdateAsync(forceCheck: true);
                if (updateInfo != null)
                {
                    _pendingUpdate = updateInfo;
                    btnUpdate.Visibility = Visibility.Visible;
                    btnUpdate.ToolTip = $"当前版本: {updateInfo.CurrentVersion}\n" +
                                       $"最新版本: {updateInfo.LatestVersion}\n" +
                                       $"文件大小: {updateInfo.FileSizeText}\n\n" +
                                       $"更新说明:\n{updateInfo.ReleaseNotes}";
                }
                else
                {
                    btnCheckUpdate.Content = "没有更新";
                    btnCheckUpdate.IsEnabled = false;
                    await Task.Delay(3000);
                    btnCheckUpdate.Content = "🔍 检查更新";
                    btnCheckUpdate.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogError("手动检查更新失败", ex);
                MyMessageBox.Show($"检查更新失败: {ex.Message}", "错误");
                btnCheckUpdate.Content = "🔍 检查更新";
                btnCheckUpdate.IsEnabled = true;
            }
        }

        // ========== 点击更新按钮 ==========
        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingUpdate == null) return;

            try
            {
                btnUpdate.IsEnabled = false;
                btnUpdate.Content = "⏳ 下载中...";

                bool success = await _updateManager.DownloadAndUpdateAsync(_pendingUpdate, progress =>
                {
                    Dispatcher.Invoke(() => btnUpdate.Content = $"⏳ 下载中 {progress}%");
                });

                if (!success)
                    MyMessageBox.Show("更新失败，请稍后重试", "错误");
            }
            catch (Exception ex)
            {
                FileLogger.LogError("更新失败", ex);
                MyMessageBox.Show($"更新失败: {ex.Message}", "错误");
                btnUpdate.Content = "🔄 有新版本可用！点击更新";
                btnUpdate.IsEnabled = true;
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 如果是托盘明确发起的退出，直接清理并允许退出
            if (_exitRequestedFromTray)
            {
                _notifyIcon?.Dispose();
                return; // 不再考虑最小化到托盘的逻辑
            }

            var vm = this.DataContext as ViewModels.MainViewModel;

            // 只有在监控时才考虑最小化到托盘的设置
            if (vm != null && vm.IsMonitoring)
            {
                var minimizeToTray = vm?.MinimizeToTray ?? false;

                if (minimizeToTray)
                {
                    e.Cancel = true;
                    this.Hide();
                    _notifyIcon.Visible = true;
                    // 通知 ViewModel 界面已隐藏，停止保存详细日志以降低内存占用
                    var vmHidden = this.DataContext as ViewModels.MainViewModel;
                    if (vmHidden != null)
                        vmHidden.UiVisible = false;
                    return;
                }

                var result = MyMessageBox.ShowDialog(
                    "正在监控。是否退出程序？\n\n是→退出\n否→后台运行并最小化到托盘\n取消→取消关闭。",
                    "正在监控",
                    MessageBoxButtonType.YesNoCancel,
                    14, // 默认字体大小
                    180, // 默认高度
                    true); // 左对齐

                if (result == true) // 对应 Yes
                {
                    try
                    {
                        vm.Stop();
                        vm.Dispose();
                        _notifyIcon.Dispose();                      
                    }
                    catch (Exception ex)
                    {
                        FileLogger.LogError("停止监控失败: ", ex);
                    }
                }
                else if (result == false) // 对应 No
                {
                    e.Cancel = true;
                    this.Hide();
                    _notifyIcon.Visible = true;
                    // 通知 ViewModel 界面已隐藏
                    var vmHidden2 = this.DataContext as ViewModels.MainViewModel;
                    if (vmHidden2 != null)
                        vmHidden2.UiVisible = false;
                }
                else // 对应 Cancel
                {
                    e.Cancel = true;
                }                
            }
            else
            {
                _notifyIcon?.Dispose();
            }
        }
    }
}
