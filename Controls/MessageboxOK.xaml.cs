using System.Windows;

namespace MyMessagebox.Controls
{
    /// <summary>
    /// MessageboxOK.xaml 的交互逻辑
    /// </summary>
    public partial class MessageboxOK : Window
    {
        public new double FontSize { get; set; } = 14;
        public MessageboxOK()
        {
            InitializeComponent();
        }
        public MessageboxOK(string message, string title, TextAlignment? textAlignment = null)
        {
            InitializeComponent();
            Title = title;
            messageText.Text = message;

            // 设置字体大小
            this.messageText.FontSize = FontSize;

            // 条件性设置对齐方式
            if (textAlignment.HasValue)
            {
                this.messageText.TextAlignment = textAlignment.Value;
                this.messageText.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
        }
        private void OK_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
