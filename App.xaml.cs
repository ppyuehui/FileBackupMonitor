using System;
using System.Linq;
using System.Threading;
using System.Windows;
using Logging;
using MyMessagebox;

namespace FileBackupMonitor
{
    public partial class App : System.Windows.Application
    {
        private static readonly Uri DarkThemeUri = new Uri("Themes/DarkTheme.xaml", UriKind.Relative);
        private static readonly Uri LightThemeUri = new Uri("Themes/LightTheme.xaml", UriKind.Relative);
        private static Mutex mutex;
        private const string MutexName = "文件备份监控助手";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            // 根据保存的设置应用主题
            try
            {
                var settings = Services.SettingsService.Load();
                ApplyTheme(settings.IsDarkTheme);
            }
            catch (Exception ex)
            {
                FileLogger.LogError("加载主题设置失败，使用默认深色主题", ex);
            }

            bool isNewInstance;
            mutex = new Mutex(true, MutexName, out isNewInstance);

            if (!isNewInstance)
            {
                MyMessageBox.Show("程序已经在运行中！", "提示");               
                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// 切换明/暗主题
        /// </summary>
        public static void ApplyTheme(bool isDark)
        {
            var themeUri = isDark ? DarkThemeUri : LightThemeUri;
            var newDict = new ResourceDictionary { Source = themeUri };

            var dicts = Current.Resources.MergedDictionaries;

            // 找到并替换颜色字典（ThemeStyles.xaml 保持不动）
            var oldTheme = dicts.FirstOrDefault(d =>
                d.Source == DarkThemeUri || d.Source == LightThemeUri);

            if (oldTheme != null)
                dicts.Remove(oldTheme);

            dicts.Add(newDict);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                if (MainWindow?.DataContext is ViewModels.MainViewModel vm)
                {
                    vm.Stop();
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogError("应用退出时停止监控失败", ex);
            }
            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            System.Exception ex = e.Exception;
            MyMessageBox.Show(ex.Message, "错误");
            FileLogger.LogError("错误", ex);
            e.Handled = true;
        }
    }
}
