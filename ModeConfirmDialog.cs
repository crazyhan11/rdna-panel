using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace RadeonTune
{
    /// <summary>显示模式确认框：倒计时结束未确认则自动回滚。</summary>
    public class ModeConfirmDialog : Window
    {
        private readonly DispatcherTimer _timer;
        private int _left;
        private bool _finished;

        /// <summary>安全结束：只有以 ShowDialog 显示且尚未结束时才写 DialogResult。</summary>
        private void Finish(bool result)
        {
            if (_finished) return;
            _finished = true;
            _timer.Stop();
            try
            {
                if (IsLoaded && IsVisible) DialogResult = result;
            }
            catch (InvalidOperationException) { /* 非模态显示时不允许设置 */ }
            Close();
        }

        public ModeConfirmDialog(int seconds, string text)
        {
            Title = "确认显示模式";
            Width = 430; SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.ToolWindow;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E));
            Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7));
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
            FontSize = 13;

            _left = seconds;
            var root = new StackPanel { Margin = new Thickness(18) };
            var msg = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
            root.Children.Add(msg);
            var countdown = new TextBlock
            {
                Text = $"{_left} 秒后自动恢复原模式",
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x0A)),
                Margin = new Thickness(0, 10, 0, 0),
            };
            root.Children.Add(countdown);

            var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            var keep = new Button { Content = "保留此模式", Width = 110, Height = 30, Margin = new Thickness(0, 0, 8, 0),
                                    Background = new SolidColorBrush(Color.FromRgb(0xD8, 0x1A, 0x1A)), Foreground = Brushes.White, BorderBrush = Brushes.Transparent };
            keep.Click += (_, __) => Finish(true);
            var back = new Button { Content = "立即恢复", Width = 100, Height = 30 };
            back.Click += (_, __) => Finish(false);
            bar.Children.Add(keep);
            bar.Children.Add(back);
            root.Children.Add(bar);

            Content = root;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, __) =>
            {
                _left--;
                countdown.Text = $"{_left} 秒后自动恢复原模式";
                if (_left <= 0) Finish(false);
            };
            _timer.Start();
            Closed += (_, __) => _timer.Stop();
        }
    }
}
