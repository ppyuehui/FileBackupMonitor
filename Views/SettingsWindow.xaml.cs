using System.Windows;

namespace FileBackupMonitor.Views
{
    public partial class SettingsWindow : Window
    {
        public ViewModels.SettingsViewModel ViewModel { get; }
        public Models.AppSettings Settings => ViewModel.Settings;

        public SettingsWindow(Models.AppSettings current)
        {
            InitializeComponent();
            ViewModel = new ViewModels.SettingsViewModel(current);
            DataContext = ViewModel;
        }
    }
}
