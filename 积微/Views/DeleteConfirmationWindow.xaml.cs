using System;
using System.Windows;
using 积微.Models;

namespace 积微.Views
{
    /// <summary>删除确认对话框窗口。</summary>
    public partial class DeleteConfirmationWindow : Window
    {
        /// <summary>获取用户是否确认了删除操作。</summary>
        public bool Confirmed { get; private set; } = false;

        public DeleteConfirmationWindow()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private void OnLoaded(object sender, EventArgs e)
        {
            App.RegisterThemeWindow(this);
        }

        private void OnClosed(object sender, EventArgs e)
        {
            App.UnregisterThemeWindow(this);
        }

        public DeleteConfirmationWindow(string title, string message, string confirmText = "确认") : this()
        {
            Title = title;
            MessageTextBlock.Text = message;
            ConfirmButton.Content = confirmText;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
