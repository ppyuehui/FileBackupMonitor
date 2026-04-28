using System;
using System.Windows;
using MyMessagebox.Controls;

namespace MyMessagebox
{
    public static class MyMessageBox
    {
        /// <summary>
        /// 按钮类型枚举
        /// </summary>
        public enum MessageBoxButtonType
        {
            OK = 0,
            OKCancel = 1,
            YesNoCancel = 2
        }

        /// <summary>
        /// 显示简单消息框（只有确定按钮）
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题</param>
        /// <param name="fontSize">字体大小</param>
        /// <param name="height">窗口高度</param>
        /// <param name="leftAlign">是否左对齐</param>
        public static void Show(
            string message,
            string title,
            double fontSize = 14,
            int height = 150,
            bool leftAlign = false)
        {
            ShowDialog(message, title, MessageBoxButtonType.OK, fontSize, height, leftAlign);
        }

        /// <summary>
        /// 显示对话框并返回结果
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题</param>
        /// <param name="buttonType">按钮类型</param>
        /// <param name="fontSize">字体大小</param>
        /// <param name="height">窗口高度</param>
        /// <param name="leftAlign">是否左对齐</param>
        /// <returns>用户选择结果</returns>
        
        public static bool? ShowDialog(
            string message,
            string title,
            MessageBoxButtonType buttonType = MessageBoxButtonType.OK,
            double fontSize = 14,
            int height = 150,
            bool leftAlign = false)
        {
            Window messageBox;
            TextAlignment? alignment = leftAlign ? TextAlignment.Left : (TextAlignment?)null;

            switch (buttonType)
            {
                case MessageBoxButtonType.OK:
                    messageBox = new MessageboxOK(message, title, alignment);
                    break;
                case MessageBoxButtonType.OKCancel:
                    messageBox = new MessageboxOKCancel(message, title, alignment);
                    break;
                case MessageBoxButtonType.YesNoCancel:
                    //messageBox = new MessageboxYesNoCancel(message, title, alignment);
                    //break;
                    var ync = new MessageboxYesNoCancel(message, title, alignment)
                    {
                        Height = height,
                        FontSize = fontSize
                    };
                    ync.ShowDialog();

                    switch (ync.Result)
                    {
                        case MessageboxYesNoCancel.YesNoCancelResult.Yes:
                            return true;
                        case MessageboxYesNoCancel.YesNoCancelResult.No:
                            return false;
                        default: // 包括Cancel和其他情况
                            return null;
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(buttonType), "不支持的消息框按钮类型");
            }

            messageBox.Height = height;

            // 设置字体大小（需要确保所有消息框都有 FontSize 属性或 messageText 控件）
            if (messageBox is MessageboxOK ok) ok.FontSize = fontSize;
            else if (messageBox is MessageboxOKCancel okCancel) okCancel.FontSize = fontSize;
            //else if (messageBox is MessageboxYesNoCancel yesNoCancel) yesNoCancel.FontSize = fontSize;

            return messageBox.ShowDialog();
        }
    }
}
