using System;
using System.Windows;
using 积微.Models;

namespace 积微.Views
{
    /// <summary>通用消息提示窗口。</summary>
    public partial class MessageBoxWindow : Window
    {
        /// <summary>获取用户是否点击了确认按钮。</summary>
        public bool Confirmed { get; private set; }

        public MessageBoxWindow(string title, string message, string buttonText = "确定")
        {
            InitializeComponent();
            Title = title;
            MessageTextBlock.Text = message;
            ConfirmButton.Content = buttonText;
            Confirmed = false;

            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            App.RegisterThemeWindow(this);
        }

        private void OnClosed(object sender, EventArgs e)
        {
            App.UnregisterThemeWindow(this);
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            Close();
        }
    }
}