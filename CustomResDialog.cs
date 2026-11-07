using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RadeonTune
{
    /// <summary>
    /// 新建自定义分辨率：字段与 AMD 面板一致。
    /// 可编辑：分辨率、刷新率、演示、时序标准、极性；
    /// 只读自动计算：像素时钟、全局刷新率、时序总计/显示/前沿/同步宽度。
    /// 越界直接拒绝（无“仍要创建”通道）。
    /// </summary>
    public class CustomResDialog : Window
    {
        private readonly EdidInfo _edid;
        private readonly string[] _cur;
        private readonly TextBox _w, _h, _hz;
        private readonly TextBox _pix, _hzOut, _hTotal, _vTotal, _hDisp, _vDisp, _hFront, _vFront, _hSync, _vSync;
        private readonly ComboBox _presentation, _timing, _hPol, _vPol;
        private readonly TextBlock _warn;
        private readonly Button _create;

        public int Width2 { get; private set; }
        public int Height2 { get; private set; }
        public int Refresh { get; private set; }
        public int Presentation { get; private set; }
        public int TimingStandard { get; private set; }
        public int PixelClock { get; private set; }
        public int HTotal { get; private set; }
        public int VTotal { get; private set; }
        public int HDisplay { get; private set; }
        public int VDisplay { get; private set; }
        public int HFront { get; private set; }
        public int VFront { get; private set; }
        public int HSync { get; private set; }
        public int VSync { get; private set; }
        public int HPol { get; private set; }
        public int VPol { get; private set; }
        public int TimingFlags { get; private set; }
        public bool Accepted { get; private set; }

        public CustomResDialog(string current, EdidInfo edid)
        {
            _edid = edid ?? new EdidInfo();
            _cur = current.Split(',');
            int I(int i, int d) => _cur.Length > i && int.TryParse(_cur[i], out var v) ? v : d;

            Title = "新的自定义分辨率";
            Width = 560;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.ToolWindow;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E));
            Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7));
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
            FontSize = 13;

            var root = new StackPanel { Margin = new Thickness(16) };

            Grid Row(string label)
            {
                var g = new Grid { Height = 34 };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.Children.Add(new TextBlock
                {
                    Text = label, VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)),
                });
                return g;
            }

            TextBox Edit(string label, int value)
            {
                var g = Row(label);
                var box = new TextBox
                {
                    Text = value.ToString(), Height = 26, Width = 150,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2C)),
                    Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0x5A, 0x5C)),
                    Padding = new Thickness(6, 0, 6, 0), HorizontalAlignment = HorizontalAlignment.Right,
                };
                Grid.SetColumn(box, 1);
                g.Children.Add(box);
                root.Children.Add(g);
                return box;
            }

            TextBox Read(string label)
            {
                var g = Row(label);
                var box = new TextBox
                {
                    IsReadOnly = true, Focusable = false, Height = 26, Width = 150,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Background = new SolidColorBrush(Color.FromRgb(0x23, 0x23, 0x25)),
                    Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
                    Padding = new Thickness(6, 0, 6, 0), HorizontalAlignment = HorizontalAlignment.Right,
                };
                Grid.SetColumn(box, 1);
                g.Children.Add(box);
                root.Children.Add(g);
                return box;
            }

            ComboBox Combo(string label, string[] items, int selected)
            {
                var g = Row(label);
                var cb = new ComboBox
                {
                    Height = 26, Width = 150, HorizontalAlignment = HorizontalAlignment.Right,
                    Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2C)),
                    Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0x5A, 0x5C)),
                };
                foreach (var it in items) cb.Items.Add(it);
                cb.SelectedIndex = Math.Clamp(selected, 0, items.Length - 1);
                Grid.SetColumn(cb, 1);
                g.Children.Add(cb);
                root.Children.Add(g);
                return cb;
            }

            _w = Edit("分辨率宽 (px)", I(0, 2560));
            _h = Edit("分辨率高 (px)", I(1, 1440));
            _hz = Edit("刷新率 (Hz)", I(2, 60));
            _presentation = Combo("演示", new[] { "逐行扫描", "隔行扫描" }, I(3, 0));
            _timing = Combo("时序标准", new[] { "CVT", "CVT - 减少消隐", "GTF", "手动" }, 1);

            _pix = Read("全局像素时钟 (kHz)");
            _hzOut = Read("全局刷新率 (Hz)");

            root.Children.Add(new TextBlock
            {
                Text = "时序信息（自动计算，只读）",
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)),
                FontSize = 11, Margin = new Thickness(0, 12, 0, 2),
            });

            _hTotal = Read("时序总计 水平");
            _vTotal = Read("时序总计 垂直");
            _hDisp = Read("时序显示 水平");
            _vDisp = Read("时序显示 垂直");
            _hFront = Read("时序前沿 水平");
            _vFront = Read("时序前沿 垂直");
            _hSync = Read("时序同步宽度 水平");
            _vSync = Read("时序同步宽度 垂直");
            _hPol = Combo("时序极性 水平", new[] { "正", "负" }, I(15, 0));
            _vPol = Combo("时序极性 垂直", new[] { "正", "负" }, I(16, 1));

            root.Children.Add(new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)),
                FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0),
                Text = _edid.MaxVHz > 0
                    ? $"EDID：{_edid.MonitorName}　刷新率 {_edid.MinVHz}–{_edid.MaxVHz} Hz　最大像素时钟 {_edid.MaxPixelClockMHz} MHz"
                    : "EDID：未读到范围限制，仅按时序标准计算",
            });

            _warn = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A)),
                FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0),
            };
            root.Children.Add(_warn);

            var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            var cancel = new Button { Content = "取消", Width = 90, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
            cancel.Click += (_, __) => { Accepted = false; DialogResult = false; Close(); };
            _create = new Button
            {
                Content = "创建", Width = 120, Height = 30,
                Background = new SolidColorBrush(Color.FromRgb(0xD8, 0x1A, 0x1A)),
                Foreground = Brushes.White, BorderBrush = Brushes.Transparent,
            };
            _create.Click += (_, __) =>
            {
                if (_warn.Text.Length > 0) return;
                Accepted = true;
                DialogResult = true;   // 必须设置，否则 ShowDialog() 返回 null，调用方会当成取消
                Close();
            };
            bar.Children.Add(cancel);
            bar.Children.Add(_create);
            root.Children.Add(bar);

            Content = root;

            _w.TextChanged += (_, __) => Recalc();
            _h.TextChanged += (_, __) => Recalc();
            _hz.TextChanged += (_, __) => Recalc();
            _timing.SelectionChanged += (_, __) => Recalc();
            Recalc();
        }

        private EdidTiming Template() => new()
        {
            HActive = int.TryParse(_cur[0], out var w) ? w : 2560,
            VActive = int.TryParse(_cur[1], out var h) ? h : 1440,
            HBlank = Math.Max(1, (int.TryParse(_cur[7], out var ht) ? ht : 2720) - (int.TryParse(_cur[0], out var w2) ? w2 : 2560)),
            VBlank = Math.Max(1, (int.TryParse(_cur[8], out var vt) ? vt : 1481) - (int.TryParse(_cur[1], out var h2) ? h2 : 1440)),
            HFront = int.TryParse(_cur[11], out var hf) ? hf : 48,
            VFront = int.TryParse(_cur[12], out var vf) ? vf : 3,
            HSync = int.TryParse(_cur[13], out var hs) ? hs : 32,
            VSync = int.TryParse(_cur[14], out var vs) ? vs : 5,
            HNegative = int.TryParse(_cur[15], out var hp) && hp == 1,
            VNegative = int.TryParse(_cur[16], out var vp) && vp == 1,
        };

        private static int MaxRefreshFor(int w, int h)
        {
            try
            {
                var buf = new System.Text.StringBuilder(16384);
                if (RtCore.rt_modes(buf, buf.Capacity) < 0) return 0;
                int best = 0;
                foreach (var e in buf.ToString().Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = e.Split(',');
                    if (p.Length == 3 && int.Parse(p[0]) == w && int.Parse(p[1]) == h)
                        best = Math.Max(best, int.Parse(p[2]));
                }
                return best;
            }
            catch { return 0; }
        }

        private static bool ModeExists(int w, int h, int hz)
        {
            try
            {
                var buf = new System.Text.StringBuilder(16384);
                if (RtCore.rt_modes(buf, buf.Capacity) < 0) return false;
                foreach (var e in buf.ToString().Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = e.Split(',');
                    if (p.Length == 3 && int.Parse(p[0]) == w && int.Parse(p[1]) == h && int.Parse(p[2]) == hz)
                        return true;
                }
            }
            catch { }
            return false;
        }

        private void Recalc()
        {
            if (!int.TryParse(_w.Text, out int w) || !int.TryParse(_h.Text, out int h) ||
                !int.TryParse(_hz.Text, out int hz) || w <= 0 || h <= 0 || hz <= 0)
            {
                _warn.Text = "宽 / 高 / 刷新率必须为正整数";
                _create.IsEnabled = false;
                return;
            }

            // UI 顺序 → ADLX 枚举：CVT=0, CVT_RB=1, GTF=2, 手动=4（DMT=3 已被驱动拒绝，移除）
            int[] stdMap = { 0, 1, 2, 4 };
            int uiStd = Math.Clamp(_timing.SelectedIndex, 0, stdMap.Length - 1);
            int std = stdMap[uiStd];
            var t = EdidInfo.Compute(w, h, hz, std, Template());

            _pix.Text = t.PixelClockKHz.ToString(CultureInfo.InvariantCulture);
            _hzOut.Text = t.Refresh.ToString("F3", CultureInfo.InvariantCulture);
            _hTotal.Text = t.HTotal.ToString(CultureInfo.InvariantCulture);
            _vTotal.Text = t.VTotal.ToString(CultureInfo.InvariantCulture);
            _hDisp.Text = w.ToString(CultureInfo.InvariantCulture);
            _vDisp.Text = h.ToString(CultureInfo.InvariantCulture);
            _hFront.Text = t.HFront.ToString(CultureInfo.InvariantCulture);
            _vFront.Text = t.VFront.ToString(CultureInfo.InvariantCulture);
            _hSync.Text = t.HSync.ToString(CultureInfo.InvariantCulture);
            _vSync.Text = t.VSync.ToString(CultureInfo.InvariantCulture);
            _hPol.SelectedIndex = t.HNegative ? 1 : 0;
            _vPol.SelectedIndex = t.VNegative ? 1 : 0;

            var problems = _edid.Validate(w, h, hz, t, MaxRefreshFor(w, h));
            if (ModeExists(w, h, hz)) problems.Add($"该模式已存在（{w}×{h} @ {hz} Hz），请修改分辨率或刷新率");
            if (w > 16384 || h > 16384) problems.Add("分辨率超出单边上限 16384");
            if (t.PixelClockKHz > 2000000) problems.Add("像素时钟超过 2 GHz");

            if (problems.Count > 0)
            {
                _warn.Text = "已拒绝：" + string.Join("；", problems);
                _create.IsEnabled = false;
                return;
            }

            _warn.Text = "";
            _create.IsEnabled = true;

            Width2 = w; Height2 = h; Refresh = hz;
            Presentation = _presentation.SelectedIndex;
            TimingStandard = std;
            PixelClock = t.PixelClockKHz;
            HTotal = t.HTotal; VTotal = t.VTotal;
            HDisplay = w; VDisplay = h;
            HFront = t.HFront; VFront = t.VFront;
            HSync = t.HSync; VSync = t.VSync;
            HPol = _hPol.SelectedIndex; VPol = _vPol.SelectedIndex;
            TimingFlags = 0;
        }
    }
}
