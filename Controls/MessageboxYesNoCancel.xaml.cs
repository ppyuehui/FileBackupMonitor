using System.Windows;

namespace MyMessagebox.Controls
{
    /// <summary>
    /// MessageboxYesNoCancel.xaml 的交互逻辑
    /// </summary>
    public partial class MessageboxYesNoCancel : Window
    {
        public enum YesNoCancelResult
        {
            Yes,
            No,
            Cancel
        }
        public new double FontSize { get; set; } = 14;  // 添加字体大小属性
        public YesNoCancelResult Result { get; private set; } = YesNoCancelResult.Cancel;
        public MessageboxYesNoCancel()
        {
            InitializeComponent();
        }
        public MessageboxYesNoCancel(string message, string title, TextAlignment? textAlignment = null)
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
        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            Result = YesNoCancelResult.Yes;
            DialogResult = true;
            Close();
        }
        private void No_Click(object sender, RoutedEventArgs e)
        {
            Result = YesNoCancelResult.No;
            DialogResult = false;
            Close();
        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = YesNoCancelResult.Cancel;
            DialogResult = null;
            Close();
        }
    }
}
