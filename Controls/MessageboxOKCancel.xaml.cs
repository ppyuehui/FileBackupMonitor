using System.Windows;

namespace MyMessagebox.Controls
{
    public partial class MessageboxOKCancel : Window
    {
        public double FontSize { get; set; } = 14;  // 添加字体大小属性
        public MessageboxOKCancel()
        {
            InitializeComponent();
        }
        public MessageboxOKCancel(string message, string title, TextAlignment? textAlignment = null)
        {
            InitializeComponent();
            Title = title;
            messageText.Text = message;
            messageText.FontSize = FontSize;

            // 处理文本对齐
            if (textAlignment.HasValue)
            {
                messageText.TextAlignment = textAlignment.Value;
                messageText.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
        }
        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
