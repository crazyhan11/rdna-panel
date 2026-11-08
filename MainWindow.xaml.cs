using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Text;

namespace RadeonTune
{
    public partial class MainWindow : Window
    {
        private static readonly (string Key, string Label)[] Items =
        {
            ("antilag", "Radeon Anti-Lag"),
            ("rsr", "Radeon Super Resolution"),
            ("fsr", "AMD FSR 超分辨率"),
            ("fg", "AMD FSR 帧生成"),
            ("afmf", "AMD Fluid Motion Frames"),
            ("chill", "Radeon Chill"),
            ("boost", "Radeon Boost"),
            ("ris2", "Image Sharpening 2"),
            ("ensync", "Radeon Enhanced Sync"),
            ("frtc", "Frame Rate Target Control"),
            ("freesync", "FreeSync / VRR"),
        };

        private readonly Dictionary<string, Border> _tracks = new();
        private readonly Dictionary<string, Ellipse> _knobs = new();
        private readonly Dictionary<string, bool> _state = new();


        private static void Trace(string msg)
        {
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "trace.log"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + Environment.NewLine);
            }
            catch { }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int dark = unchecked((int)0x001E1C1C);   // 0x00BBGGRR ≈ #1C1C1E
                DwmSetWindowAttribute(hwnd, 34 /*DWMWA_BORDER_COLOR*/, ref dark, sizeof(int));
            }
            catch { }
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public MainWindow()
        {
            Trace("ctor:begin");
            InitializeComponent();
            Trace("ctor:after-init");
            foreach (var b in new[] { NavOc, NavDisplayBasic, NavDisplayCustom, NavDisplayScale, NavProfiles })
            {
                b.Background = Brushes.Transparent;
                b.BorderThickness = new Thickness(0);
            }
            Trace("ctor:pre-buttons");
            ResetBtn.Click += async (_, __) => await ResetCurrentScope();
            MinBtn.Click += (_, __) => WindowState = WindowState.Minimized;
            Closing += (_, e2) =>
            {
                if (_forceClose) return;
                e2.Cancel = true;
                HandleCloseRequest();
            };
            MaxBtn.Click += (_, __) =>
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            };
            CloseBtn.Click += (_, __) => HandleCloseRequest();
            Trace("ctor:pre-timer");
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, __) =>
            {
                if (_busy) return;
                _busy = true;
                try { UpdateMon(); AutoSwitchByProcess(); }
                catch (Exception ex) { System.IO.File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "tick-error.log"), ex + Environment.NewLine); }
                finally { _busy = false; }
            };
            _timer = timer;

            Trace("ctor:end");
            Loaded += (_, __) =>
            {
                try { BuildRows(); BuildOcRows(); BuildProfiles(); }
                catch (Exception ex) { System.IO.File.AppendAllText(System.IO.Path.Combine(Engine.Dir, "build-error.txt"), ex.ToString()); }
                Trace("loaded:begin");
                ApplyStartPage();
                Trace("loaded:after-startpage");
                Trace("loaded:pre-load");
                Load();
                RecoverPendingMode();
                Trace("loaded:after-load-call");
                _timer?.Start();
                if (!string.IsNullOrEmpty(App.ShotPath))
                {
                    var t2 = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
                    t2.Tick += (_, __) =>
                    {
                        t2.Stop();
                        try
                        {
                            try
                            {
                                var sb = new System.Text.StringBuilder();
                                foreach (var child in GameGrid.Children)
                                {
                                    if (child is Button b)
                                    {
                                        var img = (b.Content as Grid)?.Children.OfType<System.Windows.Controls.Image>().FirstOrDefault();
                                        sb.AppendLine($"card={b.ActualWidth}x{b.ActualHeight} panel={(b.Content as FrameworkElement)?.ActualWidth}x{(b.Content as FrameworkElement)?.ActualHeight} img={img?.ActualWidth}x{img?.ActualHeight} src={(img?.Source as System.Windows.Media.Imaging.BitmapSource)?.PixelWidth}");
                                    }
                                }
                                sb.AppendLine($"wrap={GameGrid.ActualWidth} panel={GamesPanel.ActualWidth}");
                                System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "layouts.txt"), sb.ToString());
                            }
                            catch (Exception ex2)
                            {
                                System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "layouts.txt"), ex2.ToString());
                            }
                            if (App.OpenDropdown)
                            {
                                foreach (var cb in Descendants(DisplayHost).OfType<ComboBox>())
                                {
                                    cb.IsDropDownOpen = true;
                                    break;
                                }
                                System.Threading.Thread.Sleep(300);
                            }
                            var root = (FrameworkElement)Content;
                            root.UpdateLayout();
                            var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(
                                (int)root.ActualWidth, (int)root.ActualHeight, 96, 96,
                                System.Windows.Media.PixelFormats.Pbgra32);
                            bmp.Render(root);
                            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
                            var abs = System.IO.Path.GetFullPath(App.ShotPath);
                            using var fs = System.IO.File.Create(abs);
                            enc.Save(fs);
                            System.IO.File.WriteAllText(abs + ".txt", $"ok {root.ActualWidth}x{root.ActualHeight}");
                        }
                        catch (Exception ex)
                        {
                            try { System.IO.File.WriteAllText(System.IO.Path.GetFullPath(App.ShotPath) + ".txt", ex.ToString()); } catch { }
                        }
                        Application.Current.Shutdown(0);
                    };
                    t2.Start();
                }

            };
            TrayHost.Attach(this);
            // 悬停提示（ToolTip）同样走本地化
            AddHandler(System.Windows.Controls.ToolTipService.ToolTipOpeningEvent,
                new System.Windows.Controls.ToolTipEventHandler((ss, ee) =>
                {
                    if (Loc.Language != "en") return;
                    if (ee.Source is FrameworkElement fe && fe.ToolTip is string tip)
                    {
                        var tr = Loc.T(tip);
                        if (tr != tip) { fe.ToolTip = tr; ee.Handled = true; }
                    }
                }));
            LayoutUpdated += (_, __) =>
            {
                if (Loc.Language != "en") return;
                int now = Environment.TickCount;
                if (now - _lastLocApply < 300) return;
                _lastLocApply = now;
                try { Loc.Apply(this); } catch { }
            };
            SourceInitialized += (_, __) =>
            {
                var src = (System.Windows.Interop.HwndSource)System.Windows.Interop.HwndSource.FromVisual(this);
                src?.AddHook((IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled) =>
                {
                    if ((uint)msg == App.WmShowWindow)
                    {
                        TrayHost.ShowWindow();
                        handled = true;
                    }
                    return IntPtr.Zero;
                });
            };
            InitStatusAutoClear();
        }

        private string _lastAutoProcess;
        private string _scopeGame;          // null = 全局
        private List<Game> _games = new();
        private bool _syncingScope;
        private string _displaySection = "basic";   // basic | custom | scale
        private string _curSection = "basic";
        private Panel _rowHost;
        private readonly List<(FrameworkElement el, string section)> _displayRows = new();
        private bool _busy;
        private System.Windows.Threading.DispatcherTimer _timer;

        private async void AutoSwitchByProcess()
        {
            try
            {
                var hwnd = NativeMethods.GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return;
                NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0) return;
                string name = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName;
                if (name == _lastAutoProcess) return;
                var prof = Engine.FindProfileForProcess(name);
                if (prof == null) { _lastAutoProcess = name; return; }
                _lastAutoProcess = name;
                StatusText.Text = $"检测到 {name}，应用配置 {prof.Name}…";
                var r = await Task.Run(() => Engine.Apply(prof));
                StatusText.Text = r.Ok ? $"已按进程 {name} 应用 {prof.Name}" : $"进程配置应用失败：{r.Detail}";
            }
            catch { }
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern IntPtr GetForegroundWindow();
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        }

        private void UpdateMon()
        {
            var sb = new StringBuilder(256);
            if (RtCore.rt_mon(sb, sb.Capacity) != 0) return;
            var f = sb.ToString().Split('|');
            if (f.Length < 11) return;
            // 仅当窗口可见、前台、且首页监控区可见时才继续（托盘/后台/其他页面一律停）
            bool foreground = IsVisible && IsActive && WindowState != WindowState.Minimized
                              && (HeroPanel?.Visibility == Visibility.Visible);
            if (!foreground) return;
            MonClock.Text = f[0] + " MHz";
            MonEdge.Text = f[2] + " °C";
            MonHot.Text = f[5] + " °C";
            MonPower.Text = f[8] + " W";
            MonFan.Text = $"{f[7]} %";
            MonMem.Text = f[1] + " MHz";
            MonMemTemp.Text = f[3] + " °C";
            MonVolt.Text = f[4] + " mV";
            MonFanRpm.Text = f[6] + " rpm";
            MonAct.Text = f[9] + " %";
            UpdateGpuCardStats();
        }

        private void ApplyStartPage()
        {
            var want = (App.StartPage ?? "graphics").ToLowerInvariant();
            Trace("startpage:" + want);
            BuildSidebar();
            if (!string.IsNullOrEmpty(App.ScopeArg))
            {
                var g = GameLibrary.Load().FirstOrDefault(x => string.Equals(x.ExePath, App.ScopeArg, StringComparison.OrdinalIgnoreCase));
                SelectScope(App.ScopeArg, g?.Name ?? System.IO.Path.GetFileNameWithoutExtension(App.ScopeArg));
                return;
            }
            switch (want)
            {
                case "oc": Nav_Click(NavOc, null); break;
                case "display":
                    _displaySection = App.DisplayPage;
                    Nav_Click(_displaySection == "custom" ? NavDisplayCustom : _displaySection == "scale" ? NavDisplayScale : NavDisplayBasic, null);
                    break;
                case "profiles": Nav_Click(NavProfiles, null); break;
                case "games": ShowGames(); break;
                case "global": SelectScope(null, "全局"); break;
                case "graphics": SelectScope(null, "全局"); break;   // 起始页=图形设置时不能落到首页监控
                default: ShowMonitor(); break;
            }
            Loc.Apply(this);
            Dispatcher.BeginInvoke(new Action(() => Loc.Apply(this)), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            var tag = (sender as Button)?.Tag as string;
            if (tag == null) return;
            SetMonitorVisible(false);   // 任何侧栏跳转都先收起首页监控
            if (tag.StartsWith("display", StringComparison.Ordinal))
            {
                _displaySection = tag.Contains(':') ? tag.Split(':')[1] : "basic";
                SectionTitle.Text = "显示 · " + (_displaySection == "basic" ? "基本" : _displaySection == "custom" ? "自定义" : "缩放");
                DisplayPanel.Visibility = Visibility.Visible;
                GraphicsPanel.Visibility = Visibility.Collapsed;
                OcPanel.Visibility = Visibility.Collapsed;
                ProfilesPanel.Visibility = Visibility.Collapsed;
                GamesPanel.Visibility = Visibility.Collapsed;
                SetMonitorVisible(false);
                BuildDisplay();
                foreach (var b2 in new[] { NavDisplayBasic, NavDisplayCustom, NavDisplayScale, NavOc, NavProfiles })
                    b2.Background = b2.Tag as string == tag ? new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)) : Brushes.Transparent;
                return;
            }
            SectionTitle.Text = tag == "graphics" ? "图形" : tag == "oc" ? "超频" : tag == "display" ? "显示" : "配置";
            GraphicsPanel.Visibility = tag == "graphics" ? Visibility.Visible : Visibility.Collapsed;
            DisplayPanel.Visibility = Visibility.Collapsed;   // 显示页只由 display:* 分支开启
            ProfilesPanel.Visibility = tag == "profiles" ? Visibility.Visible : Visibility.Collapsed;
            OcPanel.Visibility = tag == "oc" ? Visibility.Visible : Visibility.Collapsed;

            GamesPanel.Visibility = Visibility.Collapsed;
            SetMonitorVisible(false);
            if (ResetBtn != null) ResetBtn.Visibility = Visibility.Collapsed;
            if (false)
            {
                try { BuildDisplay(); } catch (Exception ex) { StatusText.Text = "显示页读取失败：" + ex.Message; }
            }

            foreach (var b in new[] { NavOc, NavDisplayBasic, NavDisplayCustom, NavDisplayScale, NavProfiles })
            {
                bool active = (b.Tag as string) == tag;
                b.Foreground = new SolidColorBrush(active ? Color.FromRgb(0xF2,0xF2,0xF7) : Color.FromRgb(0x8E,0x8E,0x93));
                b.Background = new SolidColorBrush(active ? Color.FromRgb(0x3A,0x3A,0x3C) : Colors.Transparent);
            }
        }

        private static void RelaunchElevated()
        {
            try
            {
                var exe = Environment.ProcessPath;
                var psi = new System.Diagnostics.ProcessStartInfo(exe)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = AppContext.BaseDirectory,
                };
                System.Diagnostics.Process.Start(psi);
                Application.Current.Shutdown(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show("提权失败：" + ex.Message, "RDNA Panel");
            }
        }

        private void TitleBar_Drag(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                return;
            }
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;

            // 最大化状态下拖动标题栏：先还原再跟随鼠标移动（Windows 的常规行为）
            if (WindowState == WindowState.Maximized)
            {
                var pos = e.GetPosition(this);
                double ratio = ActualWidth > 0 ? pos.X / ActualWidth : 0.5;
                var screen = PointToScreen(pos);
                WindowState = WindowState.Normal;
                Left = screen.X - RestoreBounds.Width * ratio;
                Top = screen.Y - pos.Y;
            }
            try { DragMove(); }
            catch (InvalidOperationException) { }
        }
        private FrameworkElement MakeToggleRow(string key, string label)
        {
            var grid = new Grid { Height = 44, Margin = new Thickness(0, 0, 8, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var text = new TextBlock
            {
                Text = OverrideKeys.Contains(key) ? label + "  ·  覆盖" : label,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xEA)),
                FontSize = 12.5, Margin = new Thickness(14, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = label,
            };
            Grid.SetColumn(text, 0);
            grid.Children.Add(text);

            var track = new Border
            {
                Width = 42, Height = 24, CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromRgb(0x48, 0x48, 0x4A)),
                BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 12, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            var knob = new Ellipse
            {
                Width = 18, Height = 18, Fill = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8)),
                HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(3, 0, 0, 0),
            };
            track.Child = knob;
            track.MouseLeftButtonUp += async (_, __) => await ToggleAsync(key);
            Grid.SetColumn(track, 1);
            grid.Children.Add(track);

            var line = new Border
            {
                Height = 1, VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x35)),
                Margin = new Thickness(14, 0, 0, 0),
            };
            grid.Children.Add(line);

            AttachHint(grid, key);
            _tracks[key] = track;
            _knobs[key] = knob;
            _state[key] = false;
            return grid;
        }

        private void BuildRows()
        {
            RowsHost.Children.Clear();
            _tracks.Clear();
            _knobs.Clear();

            var groups = new (string Title, string[] Keys)[]
            {
                ("响应与延迟", new[] { "antilag", "boost", "frtc" }),
                ("画质增强", new[] { "rsr", "fsr", "ris2" }),
                ("帧生成", new[] { "fg", "afmf" }),
                ("同步与功耗", new[] { "chill", "ensync", "freesync" }),
            };

            if (_scopeGame != null)
            {
                var prof = ScopeProfile(_scopeGame, false);
                string State(string key)
                {
                    int global = RtCore.rt_toggle_get(key);
                    bool? local = prof != null && prof.Toggles.TryGetValue(key, out var v) ? v == 1 : (bool?)null;
                    return local == null
                        ? $"继承全局（全局={(global == 1 ? "开" : "关")}）"
                        : $"本游戏={(local == true ? "开" : "关")}，全局={(global == 1 ? "开" : "关")}";
                }
                RowsHost.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2C)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(10, 6, 10, 2),
                    Padding = new Thickness(12, 8, 12, 8),
                    Child = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)),
                        FontSize = 11,
                        Text = BuildOverrideText(State),
                    },
                });
            }

            foreach (var group in groups)
            {
                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x28)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(10, 6, 10, 6),
                    Padding = new Thickness(0, 4, 0, 6),
                };
                var body = new StackPanel();
                body.Children.Add(new TextBlock
                {
                    Text = group.Title,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)),
                    FontSize = 11, FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(14, 8, 0, 4),
                });

                var grid = new UniformGrid { Columns = 2 };
                foreach (var key in group.Keys)
                {
                    var label = Items.FirstOrDefault(i => i.Key == key).Label;
                    if (label == null) continue;
                    grid.Children.Add(MakeToggleRow(key, label));
                }
                body.Children.Add(grid);
                card.Child = body;
                RowsHost.Children.Add(card);
                _rowHost = body;

                if (Array.IndexOf(group.Keys, "rsr") >= 0)
                {
                    var rb = new StringBuilder(128);
                    int rr = RtCore.rt_rsr_sharp2(rb, rb.Capacity);
                    var rv = rb.ToString().Split('|');
                    int stored = rv.Length > 2 && int.TryParse(rv[2], out var st) ? st : 0;
                    AddSubSlider("RSR 锐化", 0, 100, 10, stored * 10, v =>
                    {
                        int rc = RtCore.rt_set_rsr_sharp(v);
                        if (rc != 0 && StatusText != null) StatusText.Text = $"RSR 锐化写入失败 rc={rc}";
                    }, "RSR 锐化强度：0–100%，按 10 递增；由 ADL RSR Sharpening Strength 控制。");
                    if (rr != 0 && StatusText != null) StatusText.Text = "RSR 锐化读取失败 rc=" + rr;
                }

                if (Array.IndexOf(group.Keys, "afmf") >= 0)
                {
                    var ab = new StringBuilder(64);
                    int arc = RtCore.rt_afmf(ab, ab.Capacity);
                    var av = ab.ToString().Split('|');
                    int alg = av.Length > 1 && int.TryParse(av[1], out var a1) ? a1 : 0;
                    int sm = av.Length > 2 && int.TryParse(av[2], out var s1) ? s1 : 0;
                    AddSubCombo("AFMF 算法", new[] { "自动", "增强", "标准" }, alg,
                        v => { RtCore.rt_set_afmf_algo(v); BuildRows(); },
                        "AFMF 算法：增强偏画质、标准偏性能，自动由驱动决定。");
                    AddSubCombo("AFMF 搜索模式", new[] { "自动", "标准", "高" }, sm,
                        v => { RtCore.rt_set_afmf_search(v); BuildRows(); },
                        "AFMF 搜索模式：范围越大补帧越稳，开销也越高。");
                    if (arc != 0 && StatusText != null) StatusText.Text = "AFMF 参数读取失败 rc=" + arc;
                }
            }
                    Loc.Apply(this);
        }

        private Slider _freqSlider, _powerSlider, _voltSlider, _fanSlider;
        private Slider _minFreqSlider;
        private TextBlock _fanText;
        private TextBlock _freqText, _powerText, _voltText, _voltSliderText;
        private TextBlock _freqLabel, _voltLabel, _minFreqText, _minFreqLabel;
        private int _freqMin, _freqMax, _powerMin, _powerMax;
        // RDNA4 的频率/电压是相对默认值的偏移；RDNA2(6000) 是绝对值，且额外支持最低频率。
        // 语义由 native 的 rt_oc2_caps 探测，不再按型号硬编码。
        private bool _freqAbsolute, _voltAbsolute, _minFreqSupported;
        private const int Rdna4BaseClock = 3450;

        private void ReadOcCaps()
        {
            try
            {
                var cb = new StringBuilder(64);
                if (RtCore.rt_oc2_caps(cb, cb.Capacity) == 0)
                {
                    var c = cb.ToString().Split('|');
                    if (c.Length >= 5)
                    {
                        _freqAbsolute = c[2] == "1";
                        _voltAbsolute = c[3] == "1";
                        _minFreqSupported = c[4] == "1";
                    }
                }
            }
            catch { }
        }

        private void BuildOcRows()
        {
            OcHost.Children.Clear();
            ReadOcCaps();

            _freqLabel = new TextBlock
            {
                Text = _freqAbsolute ? "核心频率上限（MHz）"
                                     : "核心频率上限（偏移，相对 3450 MHz 默认）",
                Foreground = (Brush)FindResource("Text"), FontSize = 12, Margin = new Thickness(0, 4, 0, 2)
            };
            OcHost.Children.Add(_freqLabel);
            _freqText = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(0xD8,0x1A,0x1A)),
                                        FontSize = 12, FontFamily = new FontFamily("Consolas") };
            OcHost.Children.Add(_freqText);
            _freqSlider = new Slider { Minimum = -500, Maximum = 1000, IsSnapToTickEnabled = true,
                                       TickFrequency = 5, Margin = new Thickness(0, 2, 0, 10) };
            _freqSlider.ValueChanged += (_, __) => UpdateOcText();
            OcHost.Children.Add(_freqSlider);

            if (_minFreqSupported)
            {
                _minFreqLabel = new TextBlock
                {
                    Text = "最低频率（MHz）", Foreground = (Brush)FindResource("Text"),
                    FontSize = 12, Margin = new Thickness(0, 4, 0, 2)
                };
                OcHost.Children.Add(_minFreqLabel);
                _minFreqText = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(0xD8,0x1A,0x1A)),
                                               FontSize = 12, FontFamily = new FontFamily("Consolas") };
                OcHost.Children.Add(_minFreqText);
                _minFreqSlider = new Slider { Minimum = 0, Maximum = 100, IsSnapToTickEnabled = true,
                                              TickFrequency = 5, Margin = new Thickness(0, 2, 0, 10) };
                _minFreqSlider.ValueChanged += (_, __) => UpdateOcText();
                OcHost.Children.Add(_minFreqSlider);
            }

            OcHost.Children.Add(new TextBlock
            {
                Text = "功耗上限（%）", Foreground = (Brush)FindResource("Text"),
                FontSize = 12, Margin = new Thickness(0, 4, 0, 2)
            });
            _powerText = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(0xD8,0x1A,0x1A)),
                                         FontSize = 12, FontFamily = new FontFamily("Consolas") };
            OcHost.Children.Add(_powerText);
            _powerSlider = new Slider { Minimum = -30, Maximum = 10, IsSnapToTickEnabled = true,
                                        TickFrequency = 1, Margin = new Thickness(0, 2, 0, 10) };
            _powerSlider.ValueChanged += (_, __) => UpdateOcText();
            OcHost.Children.Add(_powerSlider);

            _voltLabel = new TextBlock
            {
                Text = _voltAbsolute ? "核心电压（mV）" : "电压偏移（mV）",
                Foreground = (Brush)FindResource("Text"),
                FontSize = 12, Margin = new Thickness(0, 4, 0, 2)
            };
            OcHost.Children.Add(_voltLabel);
            _voltSliderText = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(0xD8,0x1A,0x1A)),
                                              FontSize = 12, FontFamily = new FontFamily("Consolas") };
            OcHost.Children.Add(_voltSliderText);
            _voltSlider = new Slider { Minimum = -200, Maximum = 0, IsSnapToTickEnabled = true,
                                       TickFrequency = 5, Margin = new Thickness(0, 2, 0, 10) };
            _voltSlider.ValueChanged += (_, __) => UpdateOcText();
            OcHost.Children.Add(_voltSlider);

            OcHost.Children.Add(BuildFanCurveEditor());

            _voltText = new TextBlock { Foreground = (Brush)FindResource("Muted"), FontSize = 11,
                                        Margin = new Thickness(0, 0, 0, 10) };
            OcHost.Children.Add(_voltText);

            OcHost.Children.Add(BuildOcPresetSection());

            OcHost.Children.Add(BuildBandSection());

            var apply = new Button { Content = "APPLY", Width = 90, Height = 26, HorizontalAlignment = HorizontalAlignment.Left,
                                     Background = new SolidColorBrush(Color.FromRgb(0xD8,0x1A,0x1A)),
                                     Foreground = Brushes.White, BorderBrush = Brushes.Transparent };
            apply.Click += async (_, __) => await ApplyOc();
            OcHost.Children.Add(apply);

            // 把 V/F 区块移动到频率滑块正下方（原先隔着功耗/电压/限额三块）
            if (false && _vfSectionEl != null && _freqSlider != null)
            {
                int fi = OcHost.Children.IndexOf(_freqSlider);
                if (fi >= 0)
                {
                    OcHost.Children.Remove(_vfSectionEl);
                    OcHost.Children.Insert(Math.Min(fi + 1, OcHost.Children.Count), _vfSectionEl);
                }
            }
                    Loc.Apply(this);
        }

        private bool _forceClose;
        private System.Windows.Threading.DispatcherTimer _statusClearTimer;
        private bool _statusTranslating;
        private bool _hintTranslating;
        private int _lastLocApply;

        private void InitStatusAutoClear()
        {
            _statusClearTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            _statusClearTimer.Tick += (_, __) => { _statusClearTimer.Stop(); if (StatusText != null) StatusText.Text = ""; };
            if (HintText != null)
            {
                System.ComponentModel.DependencyPropertyDescriptor
                    .FromProperty(System.Windows.Controls.TextBlock.TextProperty, typeof(System.Windows.Controls.TextBlock))
                    .AddValueChanged(HintText, (_, __) =>
                    {
                        if (Loc.Language != "en" || _hintTranslating) return;
                        var cur = HintText.Text; var tr = Loc.T(cur);
                        if (tr != cur) { _hintTranslating = true; HintText.Text = tr; _hintTranslating = false; }
                    });
            }
            if (StatusText != null)
            {
                System.ComponentModel.DependencyPropertyDescriptor
                    .FromProperty(System.Windows.Controls.TextBlock.TextProperty, typeof(System.Windows.Controls.TextBlock))
                    .AddValueChanged(StatusText, (_, __) =>
                    {
                        // 运行时消息按词典前缀翻译（如「已应用预设 X」→「Applied preset X」）
                        if (Loc.Language == "en" && !_statusTranslating)
                        {
                            var cur = StatusText.Text;
                            var tr = Loc.T(cur);
                            if (!ReferenceEquals(tr, cur) && tr != cur)
                            {
                                _statusTranslating = true;
                                StatusText.Text = tr;
                                _statusTranslating = false;
                            }
                        }
                        _statusClearTimer.Stop();
                        if (!string.IsNullOrEmpty(StatusText.Text)) _statusClearTimer.Start();
                    });
            }
        }


        private void HandleCloseRequest()
        {
            if (_forceClose) return;
            var st = Autostart.Load();
            string action = st.CloseAction;
            if (string.IsNullOrEmpty(action)) action = "ask";
            if (action == "ask")
            {
                var (choice, remember) = AskCloseAction();
                if (choice == null) return;
                if (remember) { st.CloseAction = choice; Autostart.Save(st); }
                action = choice;
            }
            if (action == "exit") { _forceClose = true; TrayHost.ExitApp(); }
            else TrayHost.HideToTray();
        }

        private (string choice, bool remember) AskCloseAction()
        {
            var win = new Window
            {
                Title = "关闭 RDNA Panel", Width = 430, SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
                WindowStyle = WindowStyle.ToolWindow, ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
                Topmost = true,
            };
            var panel = new StackPanel { Margin = new Thickness(18) };
            panel.Children.Add(new TextBlock { Text = "要最小化到托盘继续运行，还是直接退出？", FontSize = 13, TextWrapping = TextWrapping.Wrap });
            var remember = new CheckBox
            {
                Content = "记住我的选择（可在「配置 → 关闭行为」中修改）",
                Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)),
                FontSize = 11, Margin = new Thickness(0, 12, 0, 10),
            };
            panel.Children.Add(remember);
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            string result = null;
            void Close(string choice) { result = choice; win.DialogResult = true; win.Close(); }
            var trayBtn = new Button { Content = "最小化到托盘", Height = 30, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(0, 0, 8, 0) };
            trayBtn.Click += (_, __) => Close("tray");
            var exitBtn = new Button
            {
                Content = "退出程序", Height = 30, Padding = new Thickness(12, 0, 12, 0),
                Background = new SolidColorBrush(Color.FromRgb(0xD8, 0x1A, 0x1A)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
            };
            exitBtn.Click += (_, __) => Close("exit");
            row.Children.Add(trayBtn);
            row.Children.Add(exitBtn);
            panel.Children.Add(row);
            win.Content = panel;
            var ok = win.ShowDialog() == true;
            return ok ? (result, remember.IsChecked == true) : (null, false);
        }

        private void RefreshOcValues()
        {
            if (_freqSlider == null) return;
            ReadOcCaps();
            var sb = new StringBuilder(128);
            if (RtCore.rt_oc2_info(sb, sb.Capacity) != 0) return;
            var f = sb.ToString().Split('|');
            if (f.Length != 6) return;
            _freqMin = int.Parse(f[0]); _freqMax = int.Parse(f[1]);
            _powerMin = int.Parse(f[3]); _powerMax = int.Parse(f[4]);
            _freqSlider.Minimum = _freqMin; _freqSlider.Maximum = _freqMax;
            _powerSlider.Minimum = _powerMin; _powerSlider.Maximum = _powerMax;
            _freqSlider.Value = int.Parse(f[2]);
            _powerSlider.Value = int.Parse(f[5]);

            var mb = new StringBuilder(64);
            if (_minFreqSupported && _minFreqSlider != null && RtCore.rt_oc2_min(mb, mb.Capacity) == 0)
            {
                var mv = mb.ToString().Split('|');
                if (mv.Length >= 3)
                {
                    _minFreqSlider.Minimum = int.Parse(mv[0]);
                    _minFreqSlider.Maximum = int.Parse(mv[1]);
                    _minFreqSlider.Value = int.Parse(mv[2]);
                }
            }
            var vb = new StringBuilder(64);
            if (RtCore.rt_oc2_volt(vb, vb.Capacity) == 0)
            {
                var vv = vb.ToString().Split('|');
                if (vv.Length >= 3)
                {
                    _voltSlider.Minimum = int.Parse(vv[0]);
                    _voltSlider.Maximum = int.Parse(vv[1]);
                    _voltSlider.Value = int.Parse(vv[2]);
                    _voltText.Text = $"ADLX 范围 {vv[0]} … {vv[1]} mV";
                }
            }
            UpdateOcText();
        }

        private void UpdateOcText()
        {
            if (_freqText == null) return;
            int off = (int)_freqSlider.Value;
            int abs = _freqAbsolute ? off : Rdna4BaseClock + off;
            _freqText.Text = _freqAbsolute
                ? $"{abs} MHz"
                : $"{abs} MHz   (offset {off:+#;-#;0})";
            _powerText.Text = $"{_powerSlider.Value:+0;-0;0} %";
            if (_voltSliderText != null) _voltSliderText.Text = $"{(int)_voltSlider.Value} mV";
            if (_minFreqText != null && _minFreqSlider != null) _minFreqText.Text = $"{(int)_minFreqSlider.Value} MHz";
            if (_bandIdeal != null)
            {
                _bandIdeal.Text = $"{abs} MHz";
                _bandHeavy.Text = $"{abs - 100} MHz";
                _bandExtreme.Text = $"{abs - 200} MHz";
            }
        }


        private TextBlock _bandIdeal, _bandHeavy, _bandExtreme, _bandCurrent;
        private ContentControl _vfChartHost;
        private int _vfBaselineOffset;
        private int _vfDrawnOffset = int.MinValue;
        private int _vfDrawnVolt = int.MinValue;
        private System.Windows.Threading.DispatcherTimer _vfTimer;
        private (int off, int volt) _vfPending;
        private Canvas _vfCurveLayer;
        private double _vfPxPerMv = 1;
        private double _vfShiftMv;
        private int _liveVolt, _liveFreq;
        private bool _vfDragging;
        private Slider _vfMirror;
        private FrameworkElement _vfSectionEl;
        private DateTime _vfLiveAt = DateTime.MinValue;
        private DateTime _vfLastSave = DateTime.MinValue;
        private TextBlock _vfStatus;

        private FrameworkElement BandLine(string label, int delta, out TextBlock value)
        {
            var g = new Grid { Height = 26 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new TextBlock
            {
                Text = delta == int.MinValue ? label : $"{label}   −{Math.Abs(delta)} MHz",
                Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)), FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
            });
            value = new TextBlock
            {
                Text = "—", Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
                FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(value, 1);
            g.Children.Add(value);
            return g;
        }

        private FrameworkElement BuildBandSection()
        {
            var body = new StackPanel();
            body.Children.Add(new TextBlock
            {
                Text = "频率预测区间（按负载类型估算，非驱动回读）",
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)), FontSize = 11,
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6),
            });
            body.Children.Add(BandLine("理想轻负载（桌面 / 轻游戏）", 0, out _bandIdeal));
            body.Children.Add(BandLine("重负载（3DMark 类）", -100, out _bandHeavy));
            body.Children.Add(BandLine("极重算力负载（FSR 上采样 + 光追）", -200, out _bandExtreme));
            body.Children.Add(BandLine("当前实测（1 秒刷新）", int.MinValue, out _bandCurrent));
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 4, 0, 10),
                Child = body,
            };
        }


        private List<(int t, int v)> _fanPts = new();
        private Canvas _fanCanvas;
        private Slider _fanSliderDummy;

        private FrameworkElement BuildFanCurveEditor()
        {
            var wrap = new StackPanel { Margin = new Thickness(0, 4, 0, 10) };
            wrap.Children.Add(new TextBlock
            {
                Text = "风扇曲线（X 温度 °C · Y 转速 %）",
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)), FontSize = 11,
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6),
            });

            var buf = new StringBuilder(256);
            if (RtCore.rt_fan_curve(buf, buf.Capacity) == 0)
            {
                foreach (var seg in buf.ToString().Split(';'))
                {
                    var kv = seg.Split(':');
                    if (kv.Length == 2 && int.TryParse(kv[0], out var t) && int.TryParse(kv[1], out var v))
                        _fanPts.Add((t, v));
                }
            }
            if (_fanPts.Count < 2) _fanPts = new List<(int, int)> { (30, 30), (50, 45), (70, 65), (85, 85), (100, 100) };

            const double W = 560, H = 180, padL = 40, padB = 26, padT = 10, padR = 12;
            int tMin = _fanPts.Min(p => p.t), tMax = Math.Max(tMin + 1, _fanPts.Max(p => p.t));
            double X(int t) => padL + (t - tMin) / (double)(tMax - tMin) * (W - padL - padR);
            double Y(int v) => H - padB - v / 100.0 * (H - padT - padB);

            var canvas = new Canvas { Width = W, Height = H, Background = Brushes.Transparent };
            _fanCanvas = canvas;

            void Redraw()
            {
                canvas.Children.Clear();
                var grid = new SolidColorBrush(Color.FromArgb(0x30, 0x8E, 0x8E, 0x93));
                var axis = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C));
                var muted = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93));
                var accent = new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x0A));
                for (int v = 0; v <= 100; v += 25)
                {
                    canvas.Children.Add(new System.Windows.Shapes.Line { X1 = padL, Y1 = Y(v), X2 = W - padR, Y2 = Y(v), Stroke = grid });
                    var t = new TextBlock { Text = v.ToString(), Foreground = muted, FontSize = 9 };
                    Canvas.SetLeft(t, 12); Canvas.SetTop(t, Y(v) - 7);
                    canvas.Children.Add(t);
                }
                canvas.Children.Add(new System.Windows.Shapes.Line { X1 = padL, Y1 = padT, X2 = padL, Y2 = H - padB, Stroke = axis });
                canvas.Children.Add(new System.Windows.Shapes.Line { X1 = padL, Y1 = H - padB, X2 = W - padR, Y2 = H - padB, Stroke = axis });

                var line = new System.Windows.Shapes.Polyline { Stroke = accent, StrokeThickness = 2 };
                foreach (var p in _fanPts) line.Points.Add(new Point(X(p.t), Y(p.v)));
                canvas.Children.Add(line);
                foreach (var p in _fanPts)
                {
                    var dot = new Ellipse { Width = 10, Height = 10, Fill = accent, Stroke = Brushes.White, StrokeThickness = 1 };
                    Canvas.SetLeft(dot, X(p.t) - 5); Canvas.SetTop(dot, Y(p.v) - 5);
                    canvas.Children.Add(dot);
                    var lab = new TextBlock { Text = $"{p.v}%", Foreground = muted, FontSize = 9 };
                    Canvas.SetLeft(lab, X(p.t) - 10); Canvas.SetTop(lab, Y(p.v) - 22);
                    canvas.Children.Add(lab);
                }
                for (int t = tMin; t <= tMax; t += 10)
                {
                    var tb = new TextBlock { Text = t.ToString(), Foreground = muted, FontSize = 9 };
                    Canvas.SetLeft(tb, X(t) - 8); Canvas.SetTop(tb, H - padB + 5);
                    canvas.Children.Add(tb);
                }
            }
            Redraw();

            int dragIdx = -1;
            double dragStartT = 0;
            canvas.PreviewMouseLeftButtonDown += (_, e) =>
            {
                var pos = e.GetPosition(canvas);
                dragIdx = -1;
                for (int i = 0; i < _fanPts.Count; i++)
                {
                    if (Math.Abs(pos.X - X(_fanPts[i].t)) < 14 && Math.Abs(pos.Y - Y(_fanPts[i].v)) < 14)
                    { dragIdx = i; dragStartT = _fanPts[i].t; break; }
                }
                if (dragIdx >= 0) canvas.CaptureMouse();
            };
            canvas.PreviewMouseMove += (_, e) =>
            {
                if (dragIdx < 0) return;
                var pos = e.GetPosition(canvas);
                double v = (H - padB - pos.Y) / (H - padT - padB) * 100.0;
                v = Math.Clamp(Math.Round(v / 5) * 5, 20, 100);
                int tMin2 = dragIdx > 0 ? _fanPts[dragIdx - 1].t + 1 : tMin;
                int tMax2 = dragIdx < _fanPts.Count - 1 ? _fanPts[dragIdx + 1].t - 1 : tMax;
                double tRaw = tMin + (pos.X - padL) / (W - padL - padR) * (tMax - tMin);
                int tNew = Math.Clamp((int)Math.Round(tRaw), tMin2, tMax2);
                _fanPts[dragIdx] = (tNew, (int)v);
                Redraw();
                foreach (UIElement child in canvas.Children) child.IsHitTestVisible = false;
            };
            canvas.PreviewMouseLeftButtonUp += (_, __) =>
            {
                if (dragIdx >= 0) { canvas.ReleaseMouseCapture(); dragIdx = -1; }
            };

            foreach (UIElement child in canvas.Children) child.IsHitTestVisible = false;
            var view = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both, MaxHeight = 220, Child = canvas };
            wrap.Children.Add(view);

            var zbuf = new StringBuilder(32);
            if (RtCore.rt_fan_zero(zbuf, zbuf.Capacity) == 0)
            {
                var zp = zbuf.ToString().Split('|');
                bool sup = zp.Length > 0 && zp[0] == "1";
                bool on = zp.Length > 1 && zp[1] == "1";
                var chkZero = new CheckBox
                {
                    Content = sup ? "低温零转速（风扇停转）" : "低温零转速（本卡不支持）",
                    IsChecked = on, IsEnabled = sup, Margin = new Thickness(0, 6, 0, 0),
                    Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)), FontSize = 11,
                };
                chkZero.Checked += (_, __) => RtCore.rt_set_fan_zero(1);
                chkZero.Unchecked += (_, __) => RtCore.rt_set_fan_zero(0);
                wrap.Children.Add(chkZero);
            }
            // 附加项：最低声学限制 / 最低转速（ADLX 支持才显示可用）
            var ebuf = new StringBuilder(128);
            if (RtCore.rt_fan_extra(ebuf, ebuf.Capacity) == 0)
            {
                var e = ebuf.ToString().Split('|');
                if (e.Length >= 8)
                {
                    bool acSup = e[0] == "1", minSup = e[4] == "1";
                    if (acSup)
                    {
                        int lo = int.Parse(e[1]), hi = int.Parse(e[2]), cur = int.Parse(e[3]);
                        var acText = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)), FontSize = 11 };
                        acText.Text = $"最低声学限制：{cur} MHz（{lo}–{hi}）";
                        wrap.Children.Add(acText);
                        var acSlider = new Slider { Minimum = lo, Maximum = hi, Value = cur, Margin = new Thickness(0, 2, 0, 6) };
                        acSlider.ValueChanged += (_, ev) => { acText.Text = $"最低声学限制：{(int)ev.NewValue} MHz（{lo}–{hi}）"; RtCore.rt_set_fan_acoustic((int)ev.NewValue); };
                        wrap.Children.Add(acSlider);
                    }
                    if (minSup)
                    {
                        int lo = int.Parse(e[5]), hi = int.Parse(e[6]), cur = int.Parse(e[7]);
                        var mText = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)), FontSize = 11 };
                        mText.Text = $"最低风扇转速：{cur} %（{lo}–{hi}）";
                        wrap.Children.Add(mText);
                        var mSlider = new Slider { Minimum = lo, Maximum = hi, Value = cur, Margin = new Thickness(0, 2, 0, 6) };
                        mSlider.ValueChanged += (_, ev) => { mText.Text = $"最低风扇转速：{(int)ev.NewValue} %（{lo}–{hi}）"; RtCore.rt_set_fan_min((int)ev.NewValue); };
                        wrap.Children.Add(mSlider);
                    }
                    if (!acSup && !minSup)
                    {
                        wrap.Children.Add(new TextBlock
                        {
                            Text = "本卡不支持「最低声学限制 / 最低风扇转速」这两项（ADLX 报告为 unsupported）",
                            Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)), FontSize = 10, Margin = new Thickness(0, 4, 0, 0),
                        });
                    }
                }
            }
            return wrap;
        }

        private FrameworkElement BuildOcPresetSection()
        {
            var body = new StackPanel();
            body.Children.Add(new TextBlock
            {
                Text = "超频预设（保存 / 应用）",
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)),
                FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6),
            });

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            var nameCombo = new ComboBox
            {
                Width = 190, Height = 26, IsEditable = true,
                Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            var list = new StackPanel();
            void FillCombo()
            {
                var names = Engine.LoadProfiles().Keys.ToList();
                var keep = nameCombo.SelectedItem as string;
                nameCombo.Items.Clear();
                nameCombo.Items.Add("＋ 新建预设");
                foreach (var n in names) nameCombo.Items.Add(n);
                if (keep != null && names.Contains(keep)) { nameCombo.SelectedItem = keep; nameCombo.Text = keep; }
                else { nameCombo.SelectedItem = "＋ 新建预设"; nameCombo.Text = "＋ 新建预设"; }
            }

            void Reload()
            {
                list.Children.Clear();
                var all = Engine.LoadProfiles().Values.ToList();
                if (all.Count == 0)
                {
                    list.Children.Add(new TextBlock
                    {
                        Text = "尚无超频预设", Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)), FontSize = 11,
                    });
                }
                foreach (var pr in all)
                {
                    var g = new Grid { Height = 28 };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    g.Children.Add(new TextBlock
                    {
                        Text = $"{(pr.AutoApply ? "★ " : "")}{pr.Name}{(string.IsNullOrEmpty(pr.Process) ? "" : "（游戏）")}　{(_freqAbsolute ? $"频率上限 {pr.FreqOffset} MHz" : $"频率 {pr.FreqOffset:+#;-#;0} MHz")} · 电压 {pr.VoltMv} mV · 功耗 {pr.PowerPct:+0;-0;0}% · 风扇 {(!string.IsNullOrEmpty(pr.FanCurve) ? "自定义曲线" : pr.FanPct > 0 ? pr.FanPct + "%" : "自动")}",
                        Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)), FontSize = 11,
                        VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                    });
                    var btns = new StackPanel { Orientation = Orientation.Horizontal };
                    Button Mk(string text, Action act)
                    {
                        var b = new Button
                        {
                            Content = text, Height = 24, Margin = new Thickness(6, 0, 0, 0),
                            Padding = new Thickness(10, 0, 10, 0),
                            Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E)),
                            Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
                            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)), FontSize = 11,
                        };
                        b.Click += (_, __) => act();
                        return b;
                    }
                    btns.Children.Add(Mk("加载", () =>
                    {
                        _freqSlider.Value = pr.FreqOffset;
                        _powerSlider.Value = pr.PowerPct;
                        _voltSlider.Value = pr.VoltMv;
                        if (_minFreqSupported && _minFreqSlider != null && pr.MinFreq > 0)
                            _minFreqSlider.Value = pr.MinFreq;
                        if (!string.IsNullOrEmpty(pr.FanCurve))
                        {
                            _fanPts.Clear();
                            foreach (var seg in pr.FanCurve.Split(';'))
                            {
                                var kv = seg.Split(':');
                                if (kv.Length == 2 && int.TryParse(kv[0], out var tt) && int.TryParse(kv[1], out var vv))
                                    _fanPts.Add((tt, vv));
                            }
                            BuildOcRows();
                        }
                        if (StatusText != null) StatusText.Text = $"已加载预设 {pr.Name} 到滑杆（点 APPLY 生效）";
                    }));
                    btns.Children.Add(Mk(pr.AutoApply ? "开机✓" : "开机", () =>
                    {
                        var all = Engine.LoadProfiles();
                        foreach (var x in all.Values) x.AutoApply = false;   // 只允许一个开机预设
                        if (all.TryGetValue(pr.Name, out var me)) me.AutoApply = !pr.AutoApply;
                        Engine.SaveProfiles(all);
                        Reload();
                        FillCombo();
                        if (StatusText != null) StatusText.Text = me != null && me.AutoApply ? $"已设为开机自动应用：{pr.Name}" : "已取消开机自动应用";
                    }));
                    btns.Children.Add(Mk("应用", () =>
                    {
                        Task.Run(() =>
                        {
                            var r = Engine.ApplySafe(pr);
                            Dispatcher.Invoke(() =>
                            {
                                if (StatusText != null) StatusText.Text = r.Ok ? $"已应用预设 {pr.Name}" : $"应用失败：{r.Detail}";
                            });
                        });
                    }));
                    btns.Children.Add(Mk("删除", () =>
                    {
                        var all = Engine.LoadProfiles();
                        // 配置键不一定是 Name（游戏配置的键是 "game:<exe路径>"），按值匹配删除
                        Log($"preset-del: name={pr.Name} process={pr.Process}");
                        var keys = all.Where(kv => kv.Value == pr
                                          || (kv.Value.Name == pr.Name && kv.Value.Process == pr.Process))
                                      .Select(kv => kv.Key).ToList();
                        Log($"preset-del: matched keys={keys.Count}");
                        foreach (var k in keys) all.Remove(k);
                        Engine.SaveProfiles(all);
                        Reload();
                        FillCombo();
                        if (StatusText != null)
                            StatusText.Text = keys.Count > 0
                                ? $"已删除预设 {pr.Name}"
                                : $"未找到预设 {pr.Name}（键不匹配）";
                    }));
                    Grid.SetColumn(btns, 1);
                    g.Children.Add(btns);
                    list.Children.Add(g);
                }
            }
            var save = new Button
            {
                Content = "保存当前为预设", Height = 26, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(12, 0, 12, 0),
                Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)), FontSize = 12,
            };
            var chkApplyNow = new CheckBox
            {
                Content = "保存后立即应用", IsChecked = true, Margin = new Thickness(10, 0, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)), FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var chkAuto = new CheckBox
            {
                Content = "开机自动应用", Margin = new Thickness(10, 0, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)), FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
            };
            save.Click += (_, __) =>
            {
                var all = Engine.LoadProfiles();
                string typed = (nameCombo.Text ?? "").Trim();
                string selected = nameCombo.SelectedItem as string ?? "";
                bool isNew = selected.StartsWith("＋") || string.IsNullOrEmpty(selected);

                // 槽位限制：超频预设最多 5 个
                int ocCount = all.Values.Count(x => x.HasOc);
                if (isNew && ocCount >= 5 && !all.ContainsKey(typed))
                {
                    if (StatusText != null) StatusText.Text = "超频预设已达上限 5 个，请先删除或覆盖已有预设";
                    return;
                }

                string name;
                if (!string.IsNullOrEmpty(typed) && (isNew || typed != selected)) name = typed;
                else if (!isNew) name = selected;
                else
                {
                    int n = 1;
                    while (all.ContainsKey($"OC {n}")) n++;
                    name = $"OC {n}";
                }

                if (isNew && all.ContainsKey(name) && all[name].HasOc && ocCount >= 5)
                {
                    if (StatusText != null) StatusText.Text = $"名称 {name} 已存在";
                    return;
                }

                all[name] = new Profile
                {
                    Name = name,
                    HasOc = true,
                    FreqOffset = (int)_freqSlider.Value,
                    MinFreq = _minFreqSupported && _minFreqSlider != null ? (int)_minFreqSlider.Value : 0,
                    PowerPct = (int)_powerSlider.Value,
                    VoltMv = (int)_voltSlider.Value,
                    FanPct = _fanPts.Count > 0 ? _fanPts[0].v : 0,
                    FanCurve = _fanPts.Count >= 2 ? string.Join(";", _fanPts.Select(x => $"{x.t}:{x.v}")) : "",
                };
                var saved = all[name];
                saved.AutoApply = chkAuto.IsChecked == true;
                Engine.SaveProfiles(all);
                Reload();
                FillCombo();
                nameCombo.SelectedItem = name;
                nameCombo.Text = name;
                if (StatusText != null) StatusText.Text = $"已保存预设 {name}";
                if (chkApplyNow.IsChecked == true)
                {
                    var ar = Engine.ApplySafe(saved);
                    if (StatusText != null) StatusText.Text = ar.Ok ? $"已保存并应用 {name}" : $"应用失败：{ar.Detail}";
                }
            };
            row.Children.Add(nameCombo);
            row.Children.Add(save);
            row.Children.Add(chkApplyNow);
            row.Children.Add(chkAuto);
            body.Children.Add(row);
            body.Children.Add(list);
            var rollback = new Button
            {
                Content = "回退到上次应用前", Height = 24, HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(10, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)), FontSize = 11,
            };
            rollback.Click += (_, __) =>
            {
                var msg = Engine.RestoreRollback();
                if (StatusText != null) StatusText.Text = msg;
            };
            body.Children.Add(rollback);
            FillCombo();
            Reload();

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 4, 0, 10),
                Child = body,
            };
        }

        private FrameworkElement BuildOcLimitsSection()
        {
            var b = new StringBuilder(512);
            if (RtCore.rt_od8_limits(b, b.Capacity) != 0 || b.Length == 0) return new Border();
            var f = b.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string Val(string prefix) => f.FirstOrDefault(x => x.StartsWith(prefix + "="))?.Split('=')[1] ?? "—";
            bool fabs = Val("fabs") == "1";
            bool vabs = Val("vabs") == "1";
            bool minsup = Val("minsup") == "1";

            var body = new StackPanel();
            body.Children.Add(new TextBlock
            {
                Text = "驱动可调范围（ADLX 只读）",
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)),
                FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6),
            });
            void Line(string k, string v)
            {
                var g = new Grid { Height = 24 };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.Children.Add(new TextBlock { Text = k, Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
                var tb = new TextBlock { Text = v, Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(tb, 1);
                g.Children.Add(tb);
                body.Children.Add(g);
            }
            Line(fabs ? "核心频率上限范围" : "核心频率偏移范围", $"{Val("freqmin")} – {Val("freqmax")} MHz");
            if (minsup) Line("最低频率范围", $"{Val("minfreqmin")} – {Val("minfreqmax")} MHz");
            Line("功耗上限允许", $"{Val("pwrmin")} – {Val("pwrmax")} %");
            Line(vabs ? "电压范围" : "电压偏移范围", $"{Val("voltmin")} – {Val("voltmax")} mV");
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 4, 0, 10),
                Child = body,
            };
        }






        private async Task ApplyOc()
        {
            var prof = new Profile
            {
                Name = "oc",
                HasOc = true,
                FreqOffset = (int)_freqSlider.Value,
                MinFreq = _minFreqSupported && _minFreqSlider != null ? (int)_minFreqSlider.Value : 0,
                PowerPct = (int)_powerSlider.Value,
                VoltMv = (int)_voltSlider.Value,
            };
            StatusText.Text = "正在写入超频设置…";
            var r = await Task.Run(() => Engine.Apply(prof));
            if (_fanPts.Count >= 2)
            {
                var spec = string.Join(";", _fanPts.Select(p => $"{p.t}:{p.v}"));
                int fanRc = RtCore.rt_set_fan_curve(spec);
                if (fanRc != 0) StatusText.Text = $"风扇曲线写入返回 {fanRc}";
            }
            string freqTxt = _freqAbsolute
                ? $"{prof.FreqOffset} MHz（上限）"
                : $"{Rdna4BaseClock + prof.FreqOffset} MHz（offset {prof.FreqOffset:+#;-#;0}）";
            string minTxt = prof.MinFreq > 0 ? $"、最低 {prof.MinFreq} MHz" : "";
            StatusText.Text = r.Ok
                ? $"已应用：{freqTxt}{minTxt}、功耗 {prof.PowerPct:+0;-0;0}%、电压 {prof.VoltMv} mV（已回读）"
                : $"写入失败：{r.Detail}";
        }


        private TextBox _profileName;
        private TextBox _processBox;
        private ListBox _profileList;

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            if (root == null) yield break;
            int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                yield return child;
                foreach (var d in Descendants(child)) yield return d;
            }
        }

        private void BuildDisplay()
        {
            DisplayHost.Children.Clear();
            _displayRows.Clear();

            // 读取当前状态（快速，且都在 RT 内完成）
            var mode = new StringBuilder(64); RtCore.rt_mode(mode, mode.Capacity);
            var bpc = new StringBuilder(64); RtCore.rt_bpc(bpc, bpc.Capacity);
            var gam = new StringBuilder(64); RtCore.rt_gamut(gam, gam.Capacity);
            var pix = new StringBuilder(64); RtCore.rt_pixfmt(pix, pix.Capacity);
            var modesBuf = new StringBuilder(16384); int modeCount = RtCore.rt_modes(modesBuf, modesBuf.Capacity);

            var mf = mode.ToString().Split('|');
            int curW = mf.Length > 0 && int.TryParse(mf[0], out var w0) ? w0 : 0;
            int curH = mf.Length > 1 && int.TryParse(mf[1], out var h0) ? h0 : 0;
            int curHz = mf.Length > 2 && int.TryParse(mf[2], out var z0) ? z0 : 0;

            var allModes = new List<(int w, int h, int hz)>();
            foreach (var e in modesBuf.ToString().Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var p = e.Split(',');
                if (p.Length == 3 && int.TryParse(p[0], out var w) && int.TryParse(p[1], out var h) && int.TryParse(p[2], out var hz))
                    allModes.Add((w, h, hz));
            }

            var resolutions = allModes.Select(m => (m.w, m.h)).Distinct()
                                      .OrderByDescending(r => r.w * r.h).ThenBy(r => r.w)
                                      .Select(r => $"{r.w} × {r.h}").ToList();
            string curRes = $"{curW} × {curH}";

            _curSection = "basic";
            AddSectionHeader("基本");
            // 分辨率
            AddComboRow("分辨率", resolutions, curRes, (val) =>
            {
                var parts = val.Split('×');
                if (parts.Length != 2) return;
                int nw = int.Parse(parts[0].Trim()), nh = int.Parse(parts[1].Trim());
                var hz = allModes.Where(m => m.w == nw && m.h == nh).Select(m => m.hz).OrderByDescending(x => x).FirstOrDefault();
                ApplyMode(nw, nh, hz == 0 ? 60 : hz);
            });

            // 刷新率（当前分辨率）
            var hzList = allModes.Where(m => m.w == curW && m.h == curH).Select(m => m.hz)
                                 .Distinct().OrderByDescending(x => x).Select(x => x + " Hz").ToList();
            if (hzList.Count == 0) hzList.Add(curHz + " Hz");
            AddComboRow("刷新率", hzList, curHz + " Hz", (val) =>
            {
                int nz = int.Parse(val.Replace(" Hz", ""));
                ApplyMode(curW, curH, nz);
            });

            // 位深
            var bf = bpc.ToString().Split('|');
            int curBpc = bf.Length > 0 && int.TryParse(bf[0], out var b0) ? b0 : 3;
            var bpcMap = new (int val, string name)[]
            {
                (1, "6-bit"), (2, "8-bit"), (3, "10-bit"), (4, "12-bit"), (5, "14-bit"), (6, "16-bit")
            };
            var bpcChoices = bpcMap.Where(x => bf.Length > x.val && bf[x.val] == "1").Select(x => x.name).ToList();
            string curBpcName = bpcMap.FirstOrDefault(x => x.val == curBpc).name ?? "10-bit";
            AddComboRow("位深", bpcChoices, curBpcName, (val) =>
            {
                int nv = bpcMap.First(x => x.name == val).val;
                int rc = RtCore.rt_set_bpc(nv);
                var back = new StringBuilder(64); RtCore.rt_bpc(back, back.Capacity);
                StatusText.Text = rc == 0 ? $"位深已切换：{back.ToString().Split('|')[0]}" : $"位深切换失败 rc={rc}";
                BuildDisplay();
            });

            // 像素格式
            var pixNames = new[] { "RGB 4:4:4 Full", "RGB 4:4:4 Limited", "YCbCr 4:4:4", "YCbCr 4:2:2", "YCbCr 4:2:0" };
            var pixVals = new[] { 1, 4, 2, 3, 5 };
            var pf = pix.ToString().Split('|');
            int curPix = pf.Length > 0 && int.TryParse(pf[0], out var pv0) ? pv0 : 1;
            int idx = Array.IndexOf(pixVals, curPix);
            string curPixName = idx >= 0 ? pixNames[idx] : "—";
            AddComboRow("像素格式", pixNames.ToList(), curPixName, (val) =>
            {
                int i2 = Array.IndexOf(pixNames, val);
                int rc = RtCore.rt_set_pixfmt(pixVals[i2]);
                var back = new StringBuilder(64); RtCore.rt_pixfmt(back, back.Capacity);
                StatusText.Text = rc == 0 ? $"像素格式已切换：{val}" : $"像素格式切换失败 rc={rc}（该格式可能不受支持）";
                BuildDisplay();
            });

            // 只读项
            var gf = gam.ToString().Split('|');
            string gamutTxt = gf.Length >= 3 ? $"D65 ({gf[1]}/{gf[2]})（只读）" : "—";
            AddDisplayRow("色域白点", gamutTxt, null);
            var fsBuf = new StringBuilder(64);
            int fsRc = RtCore.rt_freesync(fsBuf, fsBuf.Capacity);
            var fs = fsBuf.ToString().Split('|');
            bool fsSupported = fs.Length > 0 && fs[0] == "1";
            bool fsEnabled = fs.Length > 1 && fs[1] == "1";
            // AMD 的 VRR 有多个商品名（FreeSync / Premium / Premium Pro），Windows 侧还可能显示
            // 为 Adaptive-Sync；驱动报告的都是同一能力位，这里统一按 VRR 识别。
            string fsState = fsRc != 0
                ? "读取失败（ADLX 未返回状态）"
                : !fsSupported
                    ? "未检测到 VRR 能力（FreeSync / Premium / Premium Pro / Adaptive-Sync 均未报告）"
                    : fsEnabled
                        ? "已识别为 VRR 显示器，当前已启用"
                        : "已识别为 VRR 显示器，当前未启用";
            AddComboRow("FreeSync / VRR", new List<string> { "启用", "禁用" }, fsEnabled ? "启用" : "禁用", (val) =>
            {
                int rc = RtCore.rt_set_freesync(val == "启用" ? 1 : 0);
                var back = new StringBuilder(64); RtCore.rt_freesync(back, back.Capacity);
                var b = back.ToString().Split('|');
                bool en = b.Length > 1 && b[1] == "1";
                StatusText.Text = rc == 0
                    ? $"FreeSync → {(en ? "已启用" : "已禁用")}（ADLX 回读一致）"
                    : $"FreeSync 切换失败 rc={rc}（显示器 OSD 可能未开启 FreeSync）";
                BuildDisplay();
            });
            _curSection = "scale";
            AddSectionHeader("缩放");
            // 缩放：模式 + GPU 缩放
            var scBuf = new StringBuilder(64);
            int scRc = RtCore.rt_scale(scBuf, scBuf.Capacity);
            var sc = scBuf.ToString().Split('|');
            bool scSup = sc.Length > 0 && sc[0] == "1";
            int scMode = sc.Length > 1 && int.TryParse(sc[1], out var sm0) ? sm0 : 0;
            bool gsSup = sc.Length > 2 && sc[2] == "1";
            bool gsOn = sc.Length > 3 && sc[3] == "1";
            var scNames = new List<string> { "保持纵横比", "全面板", "居中" };
            AddComboRow("缩放模式", scNames, scSup ? scNames[Math.Clamp(scMode, 0, 2)] : "不支持", (val) =>
            {
                int idx2 = scNames.IndexOf(val);
                int rc = RtCore.rt_set_scale(idx2);
                var back = new StringBuilder(64); RtCore.rt_scale(back, back.Capacity);
                var b2 = back.ToString().Split('|');
                StatusText.Text = rc == 0
                    ? $"缩放模式 → {scNames[Math.Clamp(int.Parse(b2[1]), 0, 2)]}（回读一致）"
                    : $"缩放模式切换失败 rc={rc}";
                BuildDisplay();
            });
            AddComboRow("GPU 缩放", new List<string> { "启用", "禁用" }, gsOn ? "启用" : "禁用", (val) =>
            {
                int rc = RtCore.rt_set_gpuscaling(val == "启用" ? 1 : 0);
                var back = new StringBuilder(64); RtCore.rt_scale(back, back.Capacity);
                var b2 = back.ToString().Split('|');
                StatusText.Text = rc == 0
                    ? $"GPU 缩放 → {(b2.Length > 3 && b2[3] == "1" ? "已启用" : "已禁用")}（回读一致）"
                    : $"GPU 缩放切换失败 rc={rc}" + (gsSup ? "" : "（不支持）");
                BuildDisplay();
            });

            _curSection = "custom";
            AddSectionHeader("自定义分辨率与刷新率");
            _curSection = "custom";
            // 超虚拟分辨率（VSR）
            var vsrBuf = new StringBuilder(64);
            int vsrRc = RtCore.rt_vsr(vsrBuf, vsrBuf.Capacity);
            var vsr = vsrBuf.ToString().Split('|');
            bool vsrSup = vsr.Length > 0 && vsr[0] == "1";
            bool vsrOn = vsr.Length > 1 && vsr[1] == "1";
            AddComboRow("超虚拟分辨率 (VSR)", new List<string> { "启用", "禁用" }, vsrOn ? "启用" : "禁用", (val) =>
            {
                int rc = RtCore.rt_set_vsr(val == "启用" ? 1 : 0);
                var back = new StringBuilder(64); RtCore.rt_vsr(back, back.Capacity);
                var b = back.ToString().Split('|');
                StatusText.Text = rc == 0
                    ? $"VSR → {(b.Length > 1 && b[1] == "1" ? "已启用" : "已禁用")}（回读一致）"
                    : $"VSR 切换失败 rc={rc}" + (vsrSup ? "" : "（该显示器不支持）");
                BuildDisplay();
            });

            // 自定义分辨率
            _curSection = "custom";
            var crBuf = new StringBuilder(4096);
            int crCount = RtCore.rt_customres(crBuf, crBuf.Capacity);
            string crText = crCount <= 0 ? "尚未创建（点击新建）" : $"{crCount} 项（点击新建）";

            static string CrError(int rc) => rc switch
            {
                -200 => "该分辨率/刷新率已在自定义列表中",
                -201 => "系统已支持该模式（无需创建）",
                -202 => "驱动没有真正添加该模式（列表回读缺失）",
                -96 => "驱动拒绝该时序：刷新率或像素时钟超过该分辨率上限",
                -4 => "该显示器/驱动当前不支持自定义分辨率",
                -2 => "未找到 AMD 显示接口",
                _ => $"rc={rc}",
            };

            AddDisplayRow("自定义分辨率", crText, () =>
            {
                var cur = new StringBuilder(512);
                if (RtCore.rt_cr_current(cur, cur.Capacity) != 0) { StatusText.Text = "读取当前时序失败"; return; }
                var edidBuf = new StringBuilder(8192);
                var edid = RtCore.rt_edid(edidBuf, edidBuf.Capacity) == 0
                    ? EdidInfo.Parse(edidBuf.ToString()) : null;
                var dlg = new CustomResDialog(cur.ToString(), edid) { Owner = this };
                if (dlg.ShowDialog() != true) return;
                int rc = RtCore.rt_cr_add_ex(dlg.Width2, dlg.Height2, dlg.Refresh, dlg.Presentation, dlg.TimingStandard,
                    dlg.PixelClock, dlg.TimingFlags, dlg.HTotal, dlg.VTotal, dlg.HDisplay, dlg.VDisplay,
                    dlg.HFront, dlg.VFront, dlg.HSync, dlg.VSync, dlg.HPol, dlg.VPol);
                if (rc != 0)
                {
                    StatusText.Text = $"创建失败：{CrError(rc)}";
                    BuildDisplay();
                    return;
                }
                // 创建后直接切换，避免“创建了但没生效”的观感；确认框 15 秒不确认会自动回滚
                ApplyMode(dlg.Width2, dlg.Height2, dlg.Refresh);
            });

            if (crCount > 0)
            {
                var items = crBuf.ToString().Split(';', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < items.Length; i++)
                {
                    var parts = items[i].Split(',');
                    int cw = 0, ch = 0, chz = 0;
                    bool hasMode = parts.Length >= 3 &&
                                   int.TryParse(parts[0], out cw) &&
                                   int.TryParse(parts[1], out ch) &&
                                   int.TryParse(parts[2], out chz);
                    string label = hasMode ? $"{parts[0]} × {parts[1]} @ {parts[2]} Hz" : items[i];
                    int delIdx = i;
                    if (hasMode)
                    {
                        AddDisplayRow(label, "应用", () => ApplyMode(cw, ch, chz));
                    }
                    AddDisplayRow("　删除 " + label, "删除", () =>
                    {
                        int rc = RtCore.rt_cr_del(delIdx);
                        StatusText.Text = rc == 0
                            ? $"已删除 {label}"
                            : $"删除失败：{(rc == -96 ? "驱动拒绝删除，列表可能已损坏，请用下方「修复自定义分辨率列表」后重启显卡驱动" : CrError(rc))}";
                        BuildDisplay();
                    });
                }
            }

            AddDisplayRow("修复自定义分辨率列表", "清理持久化数据", () =>
            {
                var ans = MessageBox.Show(this,
                    "将删除注册表里本显示器的自定义分辨率持久化数据（会先备份到 %LOCALAPPDATA%\\RDNA Panel）。\n\n" +
                    "清理后需要重启显卡驱动或重启系统才会生效。继续？",
                    "修复自定义分辨率", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (ans != MessageBoxResult.Yes) return;
                int n = RtCore.rt_cr_reset(1);
                StatusText.Text = n >= 0
                    ? $"已清理 {n} 个持久化条目；请重启显卡驱动或系统后生效"
                    : $"清理失败 rc={n}（需要管理员权限）";
                BuildDisplay();
            });

            _curSection = "basic";
            AddDisplayRow("VRR 状态", fsState, null);
            AddDisplayRow("能力别名", "FreeSync · FreeSync Premium · FreeSync Premium Pro · Adaptive-Sync（同一能力）", null);

            var (inferred, why) = InferVrrFromEdid();
            AddDisplayRow("VRR 等级（EDID 推断）", $"{inferred} · {why}", null);


            ApplyDisplayFilter();
                    Loc.Apply(this);
        }

        private void ApplyDisplayFilter()
        {
            foreach (var (el, sec) in _displayRows)
                el.Visibility = sec == _displaySection ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string ModeBackupPath => System.IO.Path.Combine(Engine.StoreDir, "mode-backup.txt");
        private static string ModePendingPath => System.IO.Path.Combine(Engine.StoreDir, "mode-pending.txt");

        /// <summary>启动时若发现上次切换未确认，立即恢复备份模式（黑屏/崩溃自愈）。</summary>
        private void RecoverPendingMode()
        {
            try
            {
                if (!System.IO.File.Exists(ModePendingPath)) return;
                if (System.IO.File.Exists(ModeBackupPath))
                {
                    var b = System.IO.File.ReadAllText(ModeBackupPath).Split(',');
                    if (b.Length >= 3 && int.TryParse(b[0], out var w) && int.TryParse(b[1], out var hh) && int.TryParse(b[2], out var hz))
                    {
                        RtCore.rt_set_mode(w, hh, hz);
                        StatusText.Text = $"检测到上次显示模式未确认，已恢复 {w}×{hh} @ {hz} Hz";
                    }
                }
                System.IO.File.Delete(ModePendingPath);
            }
            catch { }
        }

        public void CliApplyMode(int w, int h, int hz) => ApplyMode(w, h, hz);

        private void ApplyMode(int w, int h, int hz)
        {
            var before = new StringBuilder(64);
            RtCore.rt_mode(before, before.Capacity);
            var b = before.ToString().Split('|');
            if (b.Length < 3) return;
            int bw = int.Parse(b[0]), bh = int.Parse(b[1]), bhz = int.Parse(b[2]);

            // 写前备份 + 未确认标记：进程崩溃/黑屏时下次启动自动恢复
            try
            {
                System.IO.Directory.CreateDirectory(Engine.StoreDir);
                System.IO.File.WriteAllText(ModeBackupPath, $"{bw},{bh},{bhz}");
                System.IO.File.WriteAllText(ModePendingPath, $"{w},{h},{hz}");
            }
            catch { }

            Log($"mode: req={w}x{h}@{hz}");
            int rc = RtCore.rt_set_mode(w, h, hz);
            Log($"mode: set rc={rc}");
            var after = new StringBuilder(64);
            RtCore.rt_mode(after, after.Capacity);
            var a = after.ToString().Split('|');
            bool switched = a.Length > 1 && a[0] == w.ToString() && a[1] == h.ToString();

            if (!switched)
            {
                RtCore.rt_set_mode(bw, bh, bhz);
                try { System.IO.File.Delete(ModePendingPath); } catch { }
                StatusText.Text = $"切换失败（rc={rc}），已恢复 {bw}×{bh} @ {bhz} Hz";
                BuildDisplay();
                return;
            }

            Log($"mode: switched={switched} actual={a[0]}x{a[1]}@{a[2]}");
            var dlg = new ModeConfirmDialog(15, $"已切换到 {a[0]}×{a[1]} @ {a[2]} Hz。\n如果不能看到画面或显示异常，等待 15 秒会自动恢复，或点「立即恢复」。")
            { Owner = this };
            bool keep = dlg.ShowDialog() == true;
            Log($"mode: keep={keep}");
            if (keep)
            {
                try { System.IO.File.Delete(ModePendingPath); } catch { }
                StatusText.Text = $"已切换到 {a[0]}×{a[1]} @ {a[2]} Hz 并保留";
            }
            else
            {
                RtCore.rt_set_mode(bw, bh, bhz);
                try { System.IO.File.Delete(ModePendingPath); } catch { }
                StatusText.Text = $"已恢复 {bw}×{bh} @ {bhz} Hz";
            }
            BuildDisplay();
        }


        /// <summary>由 EDID 推断 VRR 等级（界面注明“推断”，非驱动报告）。</summary>
        private static (string label, string reason) InferVrrFromEdid()
        {
            try
            {
                var buf = new StringBuilder(8192);
                if (RtCore.rt_edid(buf, buf.Capacity) != 0) return ("未知", "无法读取 EDID");
                var info = EdidInfo.Parse(buf.ToString());
                if (info.MaxVHz == 0) return ("FreeSync", "EDID 无范围限制描述符");
                bool lfc = info.MinVHz * 2 <= info.MaxVHz;
                bool highRate = info.MaxVHz >= 120;
                bool hdr = HasHdrStaticMetadata(buf.ToString());
                string reason = $"EDID 范围 {info.MinVHz}–{info.MaxVHz} Hz，{(lfc ? "支持 LFC" : "不支持 LFC")}{(hdr ? "，含 HDR 元数据" : "")}";
                if (highRate && lfc && hdr) return ("FreeSync Premium Pro", reason);
                if (highRate && lfc) return ("FreeSync Premium", reason);
                return ("FreeSync", reason);
            }
            catch { return ("未知", "EDID 解析失败"); }
        }

        private static bool HasHdrStaticMetadata(string hex)
        {
            try
            {
                if (hex.Length < 128 * 2) return false;
                var raw = new byte[hex.Length / 2];
                for (int i = 0; i < raw.Length; i++) raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                int extCount = raw[126];
                for (int e = 0; e < extCount; e++)
                {
                    int off = 128 * (e + 1);
                    if (off + 4 >= raw.Length) break;
                    if (raw[off] != 0x02) continue;
                    int d = raw[off + 2];
                    int p2 = off + 4;
                    while (p2 < off + d && p2 < raw.Length)
                    {
                        int tag = raw[p2] >> 5;
                        int len = raw[p2] & 0x1F;
                        if (tag == 0x07 && len >= 1 && p2 + 1 < raw.Length && raw[p2 + 1] == 0x06) return true;
                        p2 += len + 1;
                    }
                }
            }
            catch { }
            return false;
        }

        private void AddSectionHeader(string title)
        {
            var head = new TextBlock
            {
                Text = title,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)),
                FontSize = 11, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(16, 14, 0, 6),
            };
            _displayRows.Add((head, _curSection));
            DisplayHost.Children.Add(head);
        }

        private void AddComboRow(string label, List<string> items, string selected, Action<string> onChanged)
        {
            var grid = new Grid { Height = 48 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(new Border
            {
                Height = 1, VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2C)),
                Margin = new Thickness(16, 0, 0, 0)
            });
            grid.Children.Add(new TextBlock
            {
                Text = label, Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center
            });

            var combo = new ComboBox
            {
                Width = 190, Height = 28, Margin = new Thickness(0, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xEA)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x35)),
            };
            foreach (var it in items) combo.Items.Add(it);
            combo.SelectedItem = items.Contains(selected) ? selected : items.FirstOrDefault();
            combo.SelectionChanged += (_, __) =>
            {
                if (combo.SelectedItem is string sv && sv != selected)
                {
                    selected = sv;
                    onChanged(sv);
                }
            };
            Grid.SetColumn(combo, 1);
            grid.Children.Add(combo);
            _displayRows.Add((grid, _curSection));
            if (DisplayTips.TryGetValue(label, out var tipText)) AttachTip(grid, tipText);
            DisplayHost.Children.Add(grid);
        }


        private void AddDisplayRow(string label, string value, Action onClick)
        {
            var grid = new Grid { Height = 48 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(new Border
            {
                Height = 1, VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromRgb(0x33,0x33,0x35)),
                Margin = new Thickness(16, 0, 0, 0)
            });
            grid.Children.Add(new TextBlock
            {
                Text = label, Foreground = new SolidColorBrush(Color.FromRgb(0xE5,0xE5,0xEA)),
                FontSize = 13, Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center
            });
            var val = new TextBlock
            {
                Text = value, Foreground = new SolidColorBrush(Color.FromRgb(0x8E,0x8E,0x93)),
                FontSize = 13, Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(val, 1);
            if (onClick != null)
            {
                val.Cursor = System.Windows.Input.Cursors.Hand;
                val.MouseLeftButtonUp += (_, __) => onClick();
                val.Foreground = new SolidColorBrush(Color.FromRgb(0x0A,0x84,0xFF));
            }
            grid.Children.Add(val);
            _displayRows.Add((grid, _curSection));
            if (DisplayTips.TryGetValue(label, out var tipText)) AttachTip(grid, tipText);
            DisplayHost.Children.Add(grid);
        }


        private FrameworkElement BuildAutostartCard()
        {
            var st = Autostart.Load();
            bool installed = Autostart.IsInstalled();
            var body = new StackPanel();
            body.Children.Add(new TextBlock
            {
                Text = "开机自启（登录后启动本程序并最小化到托盘）",
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)),
                FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6),
            });

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var chk = new CheckBox
            {
                Content = "启用", IsChecked = installed || st.Enabled, VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)), FontSize = 11,
            };
            var lbl = new TextBlock
            {
                Text = "延迟（秒）", Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)),
                FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 6, 0),
            };
            var box = new TextBox
            {
                Text = st.DelaySeconds.ToString(), Width = 80, Height = 26,
                Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
                VerticalContentAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center,
            };
            var save = new Button
            {
                Content = "保存", Height = 26, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(12, 0, 12, 0),
                Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)), FontSize = 12,
            };
            var info = new TextBlock
            {
                Text = installed ? "当前：已启用（登录时启动）" : "当前：未启用",
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)),
                FontSize = 10, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
            };
            save.Click += (_, __) =>
            {
                if (!int.TryParse(box.Text.Trim(), out var secs) || secs < 0 || secs > 3600)
                {
                    if (StatusText != null) StatusText.Text = "延迟需为 0–3600 秒之间的整数";
                    return;
                }
                string msg = chk.IsChecked == true ? Autostart.Install(secs) : Autostart.Uninstall();
                if (StatusText != null) StatusText.Text = msg;
                info.Text = Autostart.IsInstalled() ? "当前：已启用（登录时启动）" : "当前：未注册";
            };
            row.Children.Add(chk);
            row.Children.Add(lbl);
            row.Children.Add(box);
            row.Children.Add(save);
            row.Children.Add(info);
            body.Children.Add(row);

            var closeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            closeRow.Children.Add(new TextBlock
            {
                Text = Loc.T("关闭按钮行为"), Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)),
                FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0),
            });
            var closeCombo = new ComboBox { Width = 170, Height = 26, VerticalContentAlignment = VerticalAlignment.Center };
            closeCombo.Items.Add(Loc.T("每次询问"));
            closeCombo.Items.Add(Loc.T("最小化到托盘"));
            closeCombo.Items.Add(Loc.T("退出程序"));
            closeCombo.SelectedIndex = st.CloseAction == "tray" ? 1 : st.CloseAction == "exit" ? 2 : 0;
            closeCombo.SelectionChanged += (_, __) =>
            {
                var s2 = Autostart.Load();
                s2.CloseAction = closeCombo.SelectedIndex == 1 ? "tray" : closeCombo.SelectedIndex == 2 ? "exit" : "ask";
                Autostart.Save(s2);
            };
            closeRow.Children.Add(closeCombo);
            body.Children.Add(closeRow);

            var langRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            langRow.Children.Add(new TextBlock
            {
                Text = "语言", Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)),
                FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0),
            });
            var langCombo = new ComboBox { Width = 170, Height = 26, VerticalContentAlignment = VerticalAlignment.Center };
            langCombo.Items.Add(Loc.T("中文"));
            langCombo.Items.Add("English");
            langCombo.SelectedIndex = (Autostart.Load().Language == "en") ? 1 : 0;
            langCombo.SelectionChanged += (_, __) =>
            {
                var s2 = Autostart.Load();
                s2.Language = langCombo.SelectedIndex == 1 ? "en" : "zh";
                Autostart.Save(s2);
                Loc.Language = s2.Language;
                BuildSidebar();
                BuildRows();
                BuildDisplay();
                BuildOcRows();
                RefreshOcValues();
                BuildProfiles();
                if (s2.Language == "en") Loc.Apply(this); else Loc.ApplyZh(this);
                if (StatusText != null) StatusText.Text = Loc.T("语言") + ": " + (s2.Language == "en" ? "English" : "中文");
            };
            langRow.Children.Add(langCombo);
            body.Children.Add(langRow);



            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 0, 0, 10),
                Child = body,
            };
        }

        private void BuildProfiles()
        {
            ProfilesHost.Children.Clear();
            ProfilesHost.Children.Add(BuildAutostartCard());

            ProfilesHost.Children.Add(new TextBlock
            {
                Text = "配置名", Foreground = (Brush)FindResource("Text"), FontSize = 12, Margin = new Thickness(0,2,0,4)
            });
            _profileName = new TextBox
            {
                Text = "daily", Width = 220, Height = 26, HorizontalAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0x5A, 0x5C)),
                Padding = new Thickness(6, 0, 6, 0),
            };
            ProfilesHost.Children.Add(_profileName);

            var save = new Button { Content = "SAVE CURRENT", Width = 130, Height = 26, Margin = new Thickness(0,8,0,10),
                                    HorizontalAlignment = HorizontalAlignment.Left,
                                    Background = new SolidColorBrush(Color.FromRgb(0x24,0x24,0x24)),
                                    Foreground = (Brush)FindResource("Text"), BorderBrush = (Brush)FindResource("Line") };
            save.Click += (_, __) => SaveCurrentProfile();
            ProfilesHost.Children.Add(save);

            _processBox = new TextBox
            {
                Text = "", Width = 220, Height = 26, HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 6, 0, 0), VerticalContentAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0x5A, 0x5C)),
                Padding = new Thickness(6, 0, 6, 0),
            };
            ProfilesHost.Children.Add(new TextBlock { Text = "绑定进程（exe 名，不含 .exe）", Foreground = (Brush)FindResource("Muted"), FontSize = 11, Margin = new Thickness(0, 10, 0, 0) });
            ProfilesHost.Children.Add(_processBox);

            var bind = new Button { Content = "BIND PROCESS", Width = 130, Height = 26, Margin = new Thickness(0, 6, 0, 10), HorizontalAlignment = HorizontalAlignment.Left };
            bind.Click += (_, __) => BindProcess();
            ProfilesHost.Children.Add(bind);

            ProfilesHost.Children.Add(new TextBlock { Text = "游戏库", Foreground = (Brush)FindResource("Text"),
                                                      FontSize = 13, Margin = new Thickness(0, 14, 0, 6) });
            var gameBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var scanBtn = new Button { Content = "扫描游戏", Width = 96, Height = 26, Margin = new Thickness(0, 0, 8, 0),
                                       Background = new SolidColorBrush(Color.FromRgb(0x24,0x24,0x24)),
                                       Foreground = (Brush)FindResource("Text"), BorderBrush = (Brush)FindResource("Line") };
            scanBtn.Click += (_, __) =>
            {
                var games = GameLibrary.Scan();
                StatusText.Text = $"已扫描到 {games.Count} 个游戏";
                BuildProfiles();
            };
            gameBar.Children.Add(scanBtn);

            var addBtn = new Button { Content = "添加文件夹", Width = 96, Height = 26,
                                      Background = new SolidColorBrush(Color.FromRgb(0x24,0x24,0x24)),
                                      Foreground = (Brush)FindResource("Text"), BorderBrush = (Brush)FindResource("Line") };
            addBtn.Click += (_, __) =>
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "选择游戏文件夹" };
                if (dlg.ShowDialog() == true)
                {
                    var g = GameLibrary.AddFolder(dlg.FolderName);
                    StatusText.Text = g == null ? "该文件夹内没有找到 exe" : $"已添加：{g.Name}";
                    BuildProfiles();
                }
            };
            gameBar.Children.Add(addBtn);
            ProfilesHost.Children.Add(gameBar);

            var games = GameLibrary.Load();
            if (games.Count == 0)
            {
                ProfilesHost.Children.Add(new TextBlock { Text = "点「扫描游戏」自动识别 Steam / Epic，或「添加文件夹」手动指定",
                                                          Foreground = (Brush)FindResource("Muted"), FontSize = 11,
                                                          TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,2,0,8) });
            }
            foreach (var g in games)
            {
                var row = new Grid { Height = 30, Margin = new Thickness(0, 0, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var name = new TextBlock { Text = g.Name, Foreground = (Brush)FindResource("Text"), FontSize = 12,
                                           VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                name.ToolTip = g.ExePath;
                row.Children.Add(name);
                var use = new Button { Content = "配置", Width = 56, Height = 22,
                                       Background = new SolidColorBrush(Color.FromRgb(0x24,0x24,0x24)),
                                       Foreground = (Brush)FindResource("Text"), BorderBrush = (Brush)FindResource("Line") };
                var captured = g;
                use.Click += (_, __) =>
                {
                    SelectScope(captured.ExePath, captured.Name);
                    StatusText.Text = $"作用域已切到：{captured.Name}（修改只影响该游戏）";
                };
                Grid.SetColumn(use, 1);
                row.Children.Add(use);
                ProfilesHost.Children.Add(row);
            }

            _profileList = new ListBox { Height = 220, Background = new SolidColorBrush(Color.FromRgb(0x0B,0x0B,0x0B)),
                                         Foreground = (Brush)FindResource("Text"), BorderBrush = (Brush)FindResource("Line") };
            var tpl = new DataTemplate();
            var tb = new FrameworkElementFactory(typeof(TextBlock));
            tb.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            tb.SetValue(TextBlock.FontSizeProperty, 12.0);
            tb.SetValue(TextBlock.MarginProperty, new Thickness(2, 2, 2, 2));
            tb.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding());
            tpl.VisualTree = tb;
            _profileList.ItemTemplate = tpl;
            ProfilesHost.Children.Add(_profileList);

            var apply = new Button { Content = "APPLY", Width = 90, Height = 26, Margin = new Thickness(0,10,8,0),
                                     HorizontalAlignment = HorizontalAlignment.Left,
                                     Background = new SolidColorBrush(Color.FromRgb(0xD8,0x1A,0x1A)),
                                     Foreground = Brushes.White, BorderBrush = Brushes.Transparent };
            apply.Click += async (_, __) => await ApplySelectedProfile();
            ProfilesHost.Children.Add(apply);

            var del = new Button { Content = "DELETE", Width = 90, Height = 26, Margin = new Thickness(0,10,0,0),
                                   HorizontalAlignment = HorizontalAlignment.Left,
                                   Background = new SolidColorBrush(Color.FromRgb(0x24,0x24,0x24)),
                                   Foreground = (Brush)FindResource("Text"), BorderBrush = (Brush)FindResource("Line") };
            del.Click += (_, __) => DeleteSelectedProfile();
            ProfilesHost.Children.Add(del);

            RefreshProfileList();
                    Loc.Apply(this);
        }

        private void BindProcess()
        {
            var name = SelectedProfileName();
            if (name == null) { StatusText.Text = "请先选择一个配置"; return; }
            var profiles = Engine.LoadProfiles();
            if (!profiles.TryGetValue(name, out var p)) return;
            p.Process = (_processBox?.Text ?? "").Trim();
            Engine.SaveProfiles(profiles);
            RefreshProfileList();
            StatusText.Text = string.IsNullOrEmpty(p.Process)
                ? $"已解除 {name} 的进程绑定"
                : $"已把 {name} 绑定到进程 {p.Process}";
        }

        private void RefreshProfileList()
        {
            if (_profileList == null) return;
            var profiles = Engine.LoadProfiles();
            _profileList.Items.Clear();
            foreach (var kv in profiles)
            {
                string key = kv.Key;
                string title;
                if (key.StartsWith("game:", StringComparison.Ordinal))
                {
                    var exe = key.Substring(5);
                    var g = GameLibrary.Load().FirstOrDefault(x => string.Equals(x.ExePath, exe, StringComparison.OrdinalIgnoreCase));
                    title = "游戏 · " + (g?.Name ?? System.IO.Path.GetFileNameWithoutExtension(exe));
                }
                else title = key;

                var parts = new List<string> { $"{kv.Value.Toggles.Count} 项开关" };
                if (kv.Value.HasOc)
                {
                    ReadOcCaps();
                    parts.Add(_freqAbsolute ? $"频率上限 {kv.Value.FreqOffset} MHz" : $"频率 {kv.Value.FreqOffset:+#;-#;0} MHz");
                    parts.Add($"功耗 {kv.Value.PowerPct:+0;-0;0}%");
                    parts.Add($"电压 {kv.Value.VoltMv} mV");
                }
                if (!string.IsNullOrEmpty(kv.Value.Process)) parts.Add("绑定 " + System.IO.Path.GetFileName(kv.Value.Process));
                _profileList.Items.Add($"{title}\n{string.Join(" · ", parts)}");
            }
            if (_profileList.Items.Count > 0) _profileList.SelectedIndex = 0;
        }

        private string SelectedProfileName()
        {
            if (_profileList?.SelectedItem is not string s) return null;
            var first = s.Split('\n')[0].Trim();
            var profiles = Engine.LoadProfiles();
            foreach (var kv in profiles)
            {
                string key = kv.Key;
                string title;
                if (key.StartsWith("game:", StringComparison.Ordinal))
                {
                    var exe = key.Substring(5);
                    var g = GameLibrary.Load().FirstOrDefault(x => string.Equals(x.ExePath, exe, StringComparison.OrdinalIgnoreCase));
                    title = "游戏 · " + (g?.Name ?? System.IO.Path.GetFileNameWithoutExtension(exe));
                }
                else title = key;
                if (string.Equals(title, first, StringComparison.Ordinal)) return key;
            }
            return first;
        }

        private void SaveCurrentProfile()
        {
            var name = string.IsNullOrWhiteSpace(_profileName?.Text) ? "default" : _profileName.Text.Trim();
            var p = new Profile { Name = name };
            foreach (var (key, _) in Items) p.Toggles[key] = RtCore.rt_toggle_get(key) == 1 ? 1 : 0;

            var sb = new StringBuilder(128);
            if (RtCore.rt_oc2_info(sb, sb.Capacity) == 0)
            {
                var f = sb.ToString().Split('|');
                if (f.Length == 6)
                {
                    p.FreqOffset = int.Parse(f[2]);
                    p.PowerPct = int.Parse(f[5]);
                    p.HasOc = true;
                }
            }
            var sv = new StringBuilder(64);
            if (RtCore.rt_oc2_volt(sv, sv.Capacity) == 0)
            {
                var v = sv.ToString().Split('|');
                if (v.Length >= 3) p.VoltMv = int.Parse(v[2]);
            }
            var sm = new StringBuilder(64);
            if (_minFreqSupported && RtCore.rt_oc2_min(sm, sm.Capacity) == 0)
            {
                var m = sm.ToString().Split('|');
                if (m.Length >= 3) p.MinFreq = int.Parse(m[2]);
            }

            var profiles = Engine.LoadProfiles();
            profiles[name] = p;
            Engine.SaveProfiles(profiles);
            RefreshProfileList();
            StatusText.Text = $"已保存配置 {name}";
        }

        private async Task ApplySelectedProfile()
        {
            var name = SelectedProfileName();
            if (name == null) { StatusText.Text = "请先选择一个配置"; return; }
            var profiles = Engine.LoadProfiles();
            if (!profiles.TryGetValue(name, out var p)) { StatusText.Text = "配置不存在"; return; }
            var hist = Engine.Load(Engine.HistoryPath, new History());
            if (hist.Quarantined.Contains(name)) { StatusText.Text = $"{name} 已被隔离，需手动解除"; return; }

            StatusText.Text = $"正在应用 {name}…";
            var r = await Task.Run(() => Engine.Apply(p));
            StatusText.Text = r.Ok ? $"已应用 {name}（回读一致）" : $"应用失败：{r.Detail}";
        }

        private void DeleteSelectedProfile()
        {
            var name = SelectedProfileName();
            if (name == null) return;
            var profiles = Engine.LoadProfiles();
            profiles.Remove(name);
            Engine.SaveProfiles(profiles);
            RefreshProfileList();
            StatusText.Text = $"已删除配置 {name}";
        }


        private readonly List<(Button btn, string scope)> _scopeButtons = new();

        private static readonly Dictionary<string, string> Descriptions = new()
        {
            ["antilag"] = "Radeon Anti-Lag：降低 CPU 到 GPU 的排队延迟，适合竞技射击类游戏；开启后可能轻微降低平均帧率。",
            ["rsr"] = "Radeon Super Resolution：游戏内先用较低分辨率渲染，再由驱动放大到显示器分辨率，几乎所有游戏可用。",
            ["fsr"] = "AMD FSR 超分辨率：驱动级覆盖项。开启后由驱动覆盖游戏内的 FSR 设置；游戏自身不支持时无效。",
            ["fsrfg"] = "AMD FSR 帧生成：在真实帧之间插入生成帧以提高显示帧率，需要游戏支持，可能增加输入延迟。",
            ["afmf"] = "AMD Fluid Motion Frames：驱动级帧生成，不依赖游戏支持；快速移动场景可能出现轻微抖动。",
            ["chill"] = "Radeon Chill：根据画面动静动态调节帧率上限以降低功耗与温度，竞技场景下不建议开启。",
            ["boost"] = "Radeon Boost：快速移动视角时临时降低分辨率以提升帧率，静止时恢复原生分辨率。",
            ["ris2"] = "Image Sharpening 2：锐化画面细节，可在驱动层对所有游戏生效；过度锐化会产生白边。",
            ["ensync"] = "Radeon Enhanced Sync：替代传统垂直同步，减少撕裂同时避免帧率被锁死；帧率高于刷新率时有效。",
            ["frtc"] = "Frame Rate Target Control：为全局或单个游戏设定帧率上限，降低功耗与发热。",
            ["freesync"] = "FreeSync / VRR：让显示器刷新率跟随游戏帧率，消除撕裂与卡顿；需显示器与连接方式支持。",
        };

        private TextBlock _hint;

        private static readonly HashSet<string> OverrideKeys = new() { "fsr", "fg" };

        private static readonly Dictionary<string, string> DisplayTips = new()
        {
            ["分辨率"] = "从驱动枚举到的模式中选择。切换后 15 秒内不确认会自动恢复原模式。",
            ["刷新率"] = "随当前分辨率变化。高刷新率需要 DisplayPort 线材与显示器 OSD 同时开启。",
            ["位深"] = "10-bit 需要显示器与线材支持；切换后立即生效，黑屏会自动回滚。",
            ["像素格式"] = "RGB 4:4:4 为完整色度信号；YCbCr 常见于 HDMI 连接，部分格式显示器不支持。",
            ["色域白点"] = "显示器当前报告的白点色温，只读。",
            ["FreeSync / VRR"] = "让显示器刷新率跟随游戏帧率以消除撕裂；需要显示器 OSD 里也开启。",
            ["缩放模式"] = "非原生分辨率时如何铺满屏幕：保持纵横比、全面板拉伸或居中不缩放。",
            ["GPU 缩放"] = "由显卡执行缩放（关闭时交给显示器处理），影响非原生分辨率的画质。",
            ["超虚拟分辨率 (VSR)"] = "以更高分辨率渲染再缩小到屏幕，提升细节，代价是性能。",
            ["自定义分辨率"] = "点击新建：只填写分辨率与刷新率，其余时序由 EDID 与所选时序标准自动计算。",
            ["VRR 状态"] = "由 ADLX 报告的 VRR 支持与启用状态。",
            ["能力别名"] = "AMD 的 VRR 有多个商品名，驱动报告的是同一能力位。",
            ["VRR 等级（EDID 推断）"] = "驱动未暴露营销等级，这里按 EDID 的刷新率范围与 HDR 元数据推断。",
        };

        private string BuildOverrideText(Func<string, string> state)
        {
            var g = GameLibrary.Load().FirstOrDefault(x => string.Equals(x.ExePath, _scopeGame, StringComparison.OrdinalIgnoreCase));
            var hits = g == null ? new List<FsrHit>() : FsrDetector.Scan(g.WorkDir);
            string fsrLine = hits.Where(h => h.Kind == "超分").Select(h => h.Version).FirstOrDefault() is string fv && fv != ""
                ? $"游戏自带 FSR 超分运行时：{fv}"
                : "未检测到 FSR 超分运行时（可能不支持或使用驱动覆盖）";
            string fgLine = hits.Where(h => h.Kind == "帧生成").Select(h => h.Version).FirstOrDefault() is string gv && gv != ""
                ? $"游戏自带帧生成运行时：{gv}"
                : "未检测到帧生成运行时（FSR 帧生成可能不可用）";

            return "驱动级覆盖：AMD FSR 超分辨率 / FSR 帧生成 开启后会覆盖游戏内设置。\n" +
                   $"· FSR 超分：{state("fsr")}　｜　{fsrLine}\n" +
                   $"· FSR 帧生成：{state("fg")}　｜　{fgLine}";
        }

        private void AddSubSlider(string label, int min, int max, int step, int value, Action<int> onChanged, string tip)
        {
            var grid = new Grid { Height = 44, Margin = new Thickness(16, 0, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            grid.Children.Add(new TextBlock
            {
                Text = label, Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)),
                FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            });
            var slider = new Slider
            {
                Minimum = min, Maximum = max, TickFrequency = step, IsSnapToTickEnabled = true,
                Value = value, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0),
            };
            Grid.SetColumn(slider, 1);
            grid.Children.Add(slider);
            var val = new TextBlock
            {
                Text = value + "%", Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
                FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 34, 0),
            };
            Grid.SetColumn(val, 2);
            grid.Children.Add(val);
            slider.ValueChanged += (_, e) =>
            {
                int v = (int)e.NewValue;
                val.Text = v + "%";
                onChanged(v);
            };
            AttachTip(grid, tip);
            (_rowHost ?? RowsHost).Children.Add(grid);
        }

        private void AddSubCombo(string label, string[] items, int selected, Action<int> onChanged, string tip)
        {
            var grid = new Grid { Height = 40, Margin = new Thickness(16, 0, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(new TextBlock
            {
                Text = label, Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)),
                FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            });
            var cb = new ComboBox
            {
                Width = 180, Height = 26, Margin = new Thickness(0, 0, 34, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0x5A, 0x5C)),
            };
            foreach (var it in items) cb.Items.Add(it);
            cb.SelectedIndex = Math.Clamp(selected, 0, items.Length - 1);
            cb.SelectionChanged += (_, __) => { if (cb.SelectedIndex >= 0) onChanged(cb.SelectedIndex); };
            Grid.SetColumn(cb, 1);
            grid.Children.Add(cb);
            AttachTip(grid, tip);
            (_rowHost ?? RowsHost).Children.Add(grid);
        }

        private void AttachTip(FrameworkElement el, string desc)
        {
            desc = Loc.T(desc);   // 英文模式在源头取英文，避免中英同屏
            if (string.IsNullOrEmpty(desc)) return;
            el.ToolTip = desc;
            ToolTipService.SetInitialShowDelay(el, 250);
            void Apply(DependencyObject node)
            {
                if (node is FrameworkElement fe) fe.ToolTip = desc;
                int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);
                for (int i = 0; i < n; i++) Apply(System.Windows.Media.VisualTreeHelper.GetChild(node, i));
            }
            Apply(el);
            el.MouseEnter += (_, __) =>
            {
                if (HintText != null) HintText.Text = desc;
                if (TipText != null) { TipText.Text = desc; TipPopup.IsOpen = true; }
            };
            el.MouseLeave += (_, __) => { if (TipPopup != null) TipPopup.IsOpen = false; };
        }

        private void AttachHint(FrameworkElement el, string key)
        {
            if (!Descriptions.TryGetValue(key, out var desc)) return;
            AttachTip(el, desc);   // 递归挂 ToolTip + 弹出提示 + 更新顶部注释栏
        }

        private static System.Windows.Media.ImageSource ExeIcon(string exePath)
        {
            try
            {
                var info = new SHFILEINFO();
                IntPtr r = SHGetFileInfo(exePath, 0, ref info, (uint)System.Runtime.InteropServices.Marshal.SizeOf(info),
                                         SHGFI_ICON | SHGFI_SMALLICON);
                if (r == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;
                var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    info.hIcon, System.Windows.Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                DestroyIcon(info.hIcon);
                src.Freeze();
                return src;
            }
            catch { return null; }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_SMALLICON = 0x1;

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private void BuildSidebar()
        {
            ScopeHost.Children.Clear();
            _scopeButtons.Clear();

            _games = GameLibrary.Load();
            AddScopeButton("首页", "monitor", null);
            ScopeHost.Children.Add(SectionLabel("图形设置"));
            AddScopeButton("全局", "global", null);
            AddScopeButton("游戏", "games", null);


            BuildGames();
        }

        private static TextBlock SectionLabel(string text) => new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(22, 12, 0, 6),
        };

        private void AddScopeButton(string title, string key, System.Windows.Media.ImageSource icon)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(22, 0, 0, 0) };
            if (icon != null)
                content.Children.Add(new System.Windows.Controls.Image { Source = icon, Width = 16, Height = 16, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            content.Children.Add(new TextBlock { Text = title, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });

            var btn = new Button
            {
                Content = content, Height = 32, HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xEA)),
                Template = (ControlTemplate)FindResource("ScopeItem"),
            };
            btn.Click += (_, __) =>
            {
                if (key == "games") ShowGames();
                else if (key == "monitor") ShowMonitor();
                else SelectScope(null, "全局");
            };
            _scopeButtons.Add((btn, key));
            ScopeHost.Children.Add(btn);
        }


        private string _coverGameExe;

        private void InitCoverButton()
        {
            if (CoverBtn == null) return;
            CoverBtn.Click -= CoverBtn_Click;
            CoverBtn.Click += CoverBtn_Click;
            AutoCoverBtn.Click += AutoCoverBtn_Click;
        }


        /// <summary>把手选图片缩放后另存为封面副本，避免原图被移动/覆盖后失效。</summary>
        private static string SaveCoverCopy(string srcPath, string exePath)
        {
            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 600;
                bmp.UriSource = new Uri(srcPath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();

                var dir = System.IO.Path.Combine(Engine.StoreDir, "covers");
                System.IO.Directory.CreateDirectory(dir);
                using var sha = System.Security.Cryptography.SHA1.Create();
                var hash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes((exePath ?? srcPath).ToLowerInvariant())))
                                       .Replace("-", "").Substring(0, 16);
                var outPath = System.IO.Path.Combine(dir, "custom_" + hash + ".png");

                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
                using (var fs = System.IO.File.Create(outPath)) enc.Save(fs);
                return outPath;
            }
            catch { return srcPath; }
        }

        private void PickCoverFor(string exePath)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择游戏封面",
                Filter = "图片|*.png;*.jpg;*.jpeg;*.webp;*.bmp",
            };
            if (dlg.ShowDialog() != true) return;
            var games = GameLibrary.Load();
            var g = games.FirstOrDefault(x => string.Equals(x.ExePath, exePath, StringComparison.OrdinalIgnoreCase));
            if (g == null) return;
            g.CustomCover = GameLibrary.SaveCoverCopy(dlg.FileName, g.ExePath);
            GameLibrary.Save(games);
            ShowGames();
            if (StatusText != null) StatusText.Text = "已保存封面副本：" + System.IO.Path.GetFileName(g.CustomCover);
        }

        private void Log(string msg)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Engine.StoreDir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(Engine.StoreDir, "ui.log"),
                    DateTime.Now.ToString("HH:mm:ss") + " " + msg + Environment.NewLine);
            }
            catch { }
        }

        private async void AutoCoverBtn_Click(object sender, RoutedEventArgs e)
        {
            var key = CoverFetcher.LoadKey();
            if (string.IsNullOrEmpty(key))
            {
                key = AskSgdbKey();
                if (string.IsNullOrEmpty(key)) return;
                CoverFetcher.SaveKey(key);
            }
            var games = GameLibrary.Load();
            var g = games.FirstOrDefault(x => string.Equals(x.ExePath, _coverGameExe, StringComparison.OrdinalIgnoreCase));
            if (g == null) return;
            if (StatusText != null) StatusText.Text = "正在从 SteamGridDB 获取封面…";
            var path = await CoverFetcher.FetchAsync(g.Name);
            if (path == null)
            {
                if (StatusText != null) StatusText.Text = "获取失败：请检查 API Key 是否有效，或该游戏在 SteamGridDB 无条目";
                return;
            }
            g.CustomCover = path;
            GameLibrary.Save(games);
            ShowGames();
            if (StatusText != null) StatusText.Text = "已获取封面：" + System.IO.Path.GetFileName(path);
        }

        private string AskSgdbKey()
        {
            var win = new Window
            {
                Title = "SteamGridDB API Key", Width = 520, Height = 190,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)),
                WindowStyle = WindowStyle.ToolWindow,
            };
            var panel = new StackPanel { Margin = new Thickness(14) };
            panel.Children.Add(new TextBlock
            {
                Text = "粘贴 SteamGridDB API Key（免费，在 steamgriddb.com/profile/preferences/api 生成）：",
                Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)), FontSize = 11, TextWrapping = TextWrapping.Wrap,
            });
            var box = new TextBox
            {
                Height = 26, Margin = new Thickness(0, 8, 0, 10),
                Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            panel.Children.Add(box);
            var ok = new Button { Content = "保存并获取", Height = 26, HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(12, 0, 12, 0) };
            ok.Click += (_, __) => { win.DialogResult = true; win.Close(); };
            panel.Children.Add(ok);
            win.Content = panel;
            return win.ShowDialog() == true ? box.Text.Trim() : null;
        }

        private void CoverBtn_Click(object sender, RoutedEventArgs e)
        {
            Log($"cover: click exe='{_coverGameExe}' scope='{_scopeGame}'");
            if (string.IsNullOrEmpty(_coverGameExe)) _coverGameExe = _scopeGame;
            if (string.IsNullOrEmpty(_coverGameExe))
            {
                if (StatusText != null) StatusText.Text = "没有选中游戏，无法设置封面";
                return;
            }
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择游戏封面",
                Filter = "图片|*.png;*.jpg;*.jpeg;*.webp;*.bmp",
            };
            if (dlg.ShowDialog() != true) return;
            var games = GameLibrary.Load();
            var g = games.FirstOrDefault(x => string.Equals(x.ExePath, _coverGameExe, StringComparison.OrdinalIgnoreCase));
            if (g == null) { if (StatusText != null) StatusText.Text = "未找到该游戏记录"; return; }
            g.CustomCover = GameLibrary.SaveCoverCopy(dlg.FileName, g.ExePath);
            GameLibrary.Save(games);
            ShowGames();
            if (StatusText != null) StatusText.Text = "已保存封面副本：" + System.IO.Path.GetFileName(g.CustomCover);
        }

        private void SelectScope(string exePath, string title)
        {
            SetMonitorVisible(false);   // 离开首页时必须收起监控区（hero/条带/按卡面板）
            SetMonitorVisible(false);
            ResetBtn.Visibility = Visibility.Visible;
            ResetBtn.Content = exePath == null ? "恢复全局默认" : "重置此游戏";
            _coverGameExe = exePath;
            InitCoverButton();
            if (CoverBtn != null) CoverBtn.Visibility = exePath == null ? Visibility.Collapsed : Visibility.Visible;
            if (AutoCoverBtn != null) AutoCoverBtn.Visibility = exePath == null ? Visibility.Collapsed : Visibility.Visible;
            HighlightScope(exePath == null ? "global" : "games");
            _scopeGame = exePath;
            SectionTitle.Text = exePath == null ? "全局" : "游戏 › " + title;
            GamesPanel.Visibility = Visibility.Collapsed;
            GraphicsPanel.Visibility = Visibility.Visible;
            OcPanel.Visibility = Visibility.Collapsed;
            DisplayPanel.Visibility = Visibility.Collapsed;
            ProfilesPanel.Visibility = Visibility.Collapsed;
            if (exePath != null)
            {
                BuildRows();
            }
            RefreshScopeValues();
            RefreshProfileList();
        }

        private async Task ResetCurrentScope()
        {
            if (_scopeGame == null)
            {
                var prof = new Profile { Name = "reset-global", HasOc = false };
                foreach (var (key, _) in Items) prof.Toggles[key] = 0;
                var r = await Task.Run(() => Engine.Apply(prof));
                foreach (var (key, _) in Items) { _state[key] = false; Paint(key, false); }
                StatusText.Text = r.Ok ? "已恢复全局默认（全部关闭）" : "恢复失败：" + r.Detail;
                return;
            }

            var profiles = Engine.LoadProfiles();
            profiles.Remove("game:" + _scopeGame);
            Engine.SaveProfiles(profiles);
            RefreshScopeValues();
            StatusText.Text = "已重置该游戏配置，所有选项继承全局";
        }

        private void AddCoverHeader(Game g)
        {
            var frame = new Border
            {
                Width = 260, Height = 390,       // 竖版大封面（library_600x900）
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(8, 4, 8, 10),
                Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)),
                ClipToBounds = true,
            };
            var brush = new System.Windows.Media.ImageBrush
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
            };
            frame.Background = brush;
            System.Windows.Controls.Image img = null;
            RowsHost.Children.Insert(0, frame);
            Dispatcher.InvokeAsync(() => RowsScroll.ScrollToTop(), System.Windows.Threading.DispatcherPriority.Loaded);

            var cached = GameLibrary.CoverFile(g, "portrait") ?? GameLibrary.CoverFile(g);
            if (cached != null && System.IO.File.Exists(cached)) brush.ImageSource = LoadBitmap(cached);

            if (brush.ImageSource == null)
            {
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    var path = await GameLibrary.EnsureCoverAsync(g, "portrait") ?? await GameLibrary.EnsureCoverAsync(g);
                    if (path == null) return;
                    await Dispatcher.InvokeAsync(() => brush.ImageSource = LoadBitmap(path));
                });
            }
        }

        private static System.Windows.Media.Imaging.BitmapImage LoadBitmap(string path, int decodeWidth = 600)
        {
            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
                // 限制解码尺寸：壁纸级大图直接按原尺寸进纹理会拖垮渲染线程（UCEERR_RENDERTHREADFAILURE）
                bmp.DecodePixelWidth = decodeWidth;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        private void HighlightScope(string key)
        {
            foreach (var (btn, scope) in _scopeButtons)
            {
                bool active = string.Equals(scope, key, StringComparison.OrdinalIgnoreCase);
                btn.Background = active ? new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)) : Brushes.Transparent;
            }
        }

        private void SetMonitorVisible(bool on)
        {
            var v = on ? Visibility.Visible : Visibility.Collapsed;
            if (HomePanel != null) HomePanel.Visibility = v;   // 首页内容整体切换
            CardHost.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        }


        private readonly Dictionary<int, TextBlock> _gpuCardStats = new();

        private void UpdateGpuCardStats()
        {
            var cfg = Autostart.Load();
            bool allowOthers = cfg.PollOtherGpus;
            foreach (var kv in _gpuCardStats)
            {
                if (!allowOthers && kv.Key != cfg.GpuIndex) { kv.Value.Text = "—"; continue; }
                var sb = new StringBuilder(256);
                if (RtCore.rt_mon_at(kv.Key, sb, sb.Capacity) != 0) continue;
                var f = sb.ToString().Split('|');
                if (f.Length < 10) continue;
                kv.Value.Text = $"{f[0]} MHz · {f[2]} ℃ · {f[8]} W · {f[9]}%";
            }
        }

        private void BuildGpuSelector()
        {
            if (GpuSelectorHost == null) return;
            GpuSelectorHost.Children.Clear();
            int n = 0;
            try { n = RtCore.rt_gpu_count(); } catch { }
            if (n <= 0) return;
            int sel = (Autostart.Load().GpuIndex);
            if (sel < 0 || sel >= n)
            {
                // 默认选独显（名称含 RX）
                for (int i = 0; i < n; i++)
                {
                    var b0 = new StringBuilder(160);
                    if (RtCore.rt_gpu_name(i, b0, b0.Capacity) == 0 && b0.ToString().Contains("RX "))
                    { sel = i; break; }
                }
            }
            if (sel < 0) { sel = 0; }
            {
                var cfgSel = Autostart.Load();
                if (cfgSel.GpuIndex != sel) { cfgSel.GpuIndex = sel; Autostart.Save(cfgSel); }
            }
            for (int i = 0; i < n; i++)
            {
                var b = new StringBuilder(200);
                RtCore.rt_gpu_name(i, b, b.Capacity);
                var parts = b.ToString().Split('|');
                string name = parts.Length > 0 ? parts[0] : $"GPU {i}";
                string dev = parts.Length > 1 ? "0x" + parts[1] : "";
                bool on = i == sel;
                var card = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(14, 10, 14, 10),
                    Margin = new Thickness(0, 0, 10, 0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Background = new SolidColorBrush(on ? Color.FromRgb(0x33, 0x20, 0x22) : Color.FromRgb(0x22, 0x22, 0x24)),
                    BorderBrush = new SolidColorBrush(on ? Color.FromRgb(0xE8, 0x20, 0x37) : Color.FromRgb(0x33, 0x33, 0x35)),
                    BorderThickness = new Thickness(on ? 1.5 : 1),
                };
                var st = new StackPanel();
                st.Children.Add(new TextBlock
                {
                    Text = name, Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)),
                    FontSize = 12, FontWeight = FontWeights.SemiBold,
                });
                st.Children.Add(new TextBlock
                {
                    Text = dev + (on ? "  ·  " + Loc.T("当前使用") : ""),
                    Foreground = new SolidColorBrush(on ? Color.FromRgb(0xE8, 0x20, 0x37) : Color.FromRgb(0x8E, 0x8E, 0x93)),
                    FontSize = 10, Margin = new Thickness(0, 2, 0, 0),
                });
                var stats = new TextBlock
                {
                    Text = "—", Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)),
                    FontSize = 10, Margin = new Thickness(0, 3, 0, 0),
                };
                st.Children.Add(stats);
                _gpuCardStats[i] = stats;
                card.Child = st;
                int idx = i;
                card.MouseLeftButtonUp += (_, __) =>
                {
                    if (RtCore.rt_gpu_select(idx) == 0)
                    {
                        var cfg = Autostart.Load();
                        cfg.GpuIndex = idx;
                        Autostart.Save(cfg);
                        _gpuCardStats.Clear();
            BuildGpuSelector();
            BuildGpuPanels();
                        BuildRows();
                        BuildDisplay();
                        BuildOcRows();
                        RefreshOcValues();
                        BuildProfiles();
                        Loc.Apply(this);
                        if (StatusText != null) StatusText.Text = "已切换到 " + name;
                    }
                };
                GpuSelectorHost.Children.Add(card);
            }
        }






        private readonly List<(int idx, TextBlock[] cells)> _gpuPanels = new();
        private StackPanel _gpuPanelsHost;

        private void BuildGpuPanels()
        {
            if (OtherGpuHost == null) return;
            var host = OtherGpuHost;
            host.Children.Clear();
            _gpuPanels.Clear();
            int n = 0;
            try { n = RtCore.rt_gpu_count(); } catch { }
            for (int i = 0; i < n; i++)
            {
                var nameBuf = new StringBuilder(200);
                RtCore.rt_gpu_name(i, nameBuf, nameBuf.Capacity);
                string name = nameBuf.ToString().Split('|')[0];

                var wrap = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
                wrap.Children.Add(new TextBlock
                {
                    Text = name, Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)),
                    FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 0, 0, 4),
                });

                var row = new Grid();
                string[] caps = { "核心频率", "边缘温度", "热点温度", "整卡功耗", "显存频率", "显存温度", "GPU 电压", "风扇转速", "GFX 活动" };
                var cells = new TextBlock[caps.Length];
                for (int c = 0; c < caps.Length; c++)
                {
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    var sp = new StackPanel { Margin = new Thickness(c == 0 ? 14 : 8, 8, 8, 8) };
                    sp.Children.Add(new TextBlock
                    {
                        Text = caps[c], Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)), FontSize = 10,
                    });
                    var val = new TextBlock
                    {
                        Text = "—", Foreground = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xEA)), FontSize = 13,
                        Margin = new Thickness(0, 2, 0, 0),
                    };
                    sp.Children.Add(val);
                    Grid.SetColumn(sp, c);
                    row.Children.Add(sp);
                    cells[c] = val;
                }
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x23, 0x23, 0x25)),
                    CornerRadius = new CornerRadius(10),
                };
                border.Child = row;
                wrap.Children.Add(border);
                host.Children.Add(wrap);
                _gpuPanels.Add((i, cells));
            }
        
            var pollRow = new CheckBox
            {
                Content = "同时轮询其他显卡（影响 CPU C-state）",
                IsChecked = Autostart.Load().PollOtherGpus,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)),
                FontSize = 10, Margin = new Thickness(2, 2, 0, 0),
            };
            pollRow.Checked += (_, __) => { var c2 = Autostart.Load(); c2.PollOtherGpus = true; Autostart.Save(c2); };
            pollRow.Unchecked += (_, __) => { var c2 = Autostart.Load(); c2.PollOtherGpus = false; Autostart.Save(c2); };
            if (n > 1) host.Children.Add(pollRow);
            UpdateGpuPanels();
}

        private DateTime _otherGpuPolledAt = DateTime.MinValue;

        private void UpdateGpuPanels()
        {
            if (OtherGpuHost == null || OtherGpuHost.Visibility != Visibility.Visible) return;
            var cfg = Autostart.Load();
            int sel = cfg.GpuIndex;
            bool allowOthers = cfg.PollOtherGpus;
            bool otherTick = (DateTime.Now - _otherGpuPolledAt).TotalSeconds >= 5;
            if (otherTick) _otherGpuPolledAt = DateTime.Now;

            foreach (var (idx, cells) in _gpuPanels)
            {
                bool isSelected = idx == sel;
                if (!isSelected && (!allowOthers || !otherTick))
                {
                    if (!allowOthers)
                        for (int k = 0; k < cells.Length; k++) cells[k].Text = "—";
                    continue;
                }
                var sb = new StringBuilder(256);
                if (RtCore.rt_mon_at(idx, sb, sb.Capacity) != 0) continue;
                var f = sb.ToString().Split('|');
                if (f.Length < 10) continue;
                cells[0].Text = f[0] + " MHz";
                cells[1].Text = f[2] + " ℃";
                cells[2].Text = f[5] + " ℃";
                cells[3].Text = f[8] + " W";
                cells[4].Text = f[1] + " MHz";
                cells[5].Text = f[3] + " ℃";
                cells[6].Text = f[4] + " mV";
                cells[7].Text = f[6] + " rpm";
                cells[8].Text = f[9] + " %";
            }
        }

        private void ShowMonitor()
        {
            HighlightScope("monitor");
            SectionTitle.Text = "首页";
            SetMonitorVisible(true);
            BuildGpuSelector();
            BuildGpuPanels();
            GamesPanel.Visibility = Visibility.Collapsed;
            GraphicsPanel.Visibility = Visibility.Collapsed;
            OcPanel.Visibility = Visibility.Collapsed;
            DisplayPanel.Visibility = Visibility.Collapsed;
            ProfilesPanel.Visibility = Visibility.Collapsed;
            ResetBtn.Visibility = Visibility.Collapsed;
            BuildGpuSelector();
        }

        private void ShowGames()
        {
            SetMonitorVisible(false);
            ResetBtn.Visibility = Visibility.Collapsed;
            HighlightScope("games");
            SectionTitle.Text = "游戏";
            GamesPanel.Visibility = Visibility.Visible;
            GraphicsPanel.Visibility = Visibility.Collapsed;
            OcPanel.Visibility = Visibility.Collapsed;
            DisplayPanel.Visibility = Visibility.Collapsed;
            ProfilesPanel.Visibility = Visibility.Collapsed;
            BuildGameGrid(GameSearch.Text ?? "");
        }

        private void BuildGames()
        {
            AddGameBtn.Click += (_, __) =>
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "选择游戏文件夹" };
                if (dlg.ShowDialog() != true) return;
                var g = GameLibrary.AddFolder(dlg.FolderName);
                StatusText.Text = g == null ? "该文件夹内没有找到 exe" : $"已添加：{g.Name}";
                BuildGameGrid(GameSearch.Text ?? "");
            };
            ResetAllGamesBtn.Click += (_, __) =>
            {
                var profiles = Engine.LoadProfiles();
                var keys = profiles.Keys.Where(k => k.StartsWith("game:")).ToList();
                foreach (var k in keys) profiles.Remove(k);
                Engine.SaveProfiles(profiles);
                StatusText.Text = $"已重置 {keys.Count} 个游戏配置（全部继承全局）";
            };
            GameSearch.TextChanged += (_, __) =>
            {
                GameSearchHint.Visibility = string.IsNullOrEmpty(GameSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
                BuildGameGrid(GameSearch.Text ?? "");
            };
        }

        private void BuildGameGrid(string filter)
        {
            GameGrid.Children.Clear();
            var games = GameLibrary.Load();
            if (!string.IsNullOrWhiteSpace(filter))
                games = games.Where(g => g.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var g in games)
            {
                var card = new Button
                {
                    Width = 190, Height = 106, Margin = new Thickness(6),
                    Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E)),
                    BorderThickness = new Thickness(1),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch,
                    Template = (ControlTemplate)FindResource("GameCard"),
                };
                var panel = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
                // 用 ImageBrush 做封面：Stretch=UniformToFill + AlignmentX/Y=Center，裁切一定居中
                var coverBrush = new System.Windows.Media.ImageBrush
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                };
                var cover = new System.Windows.Controls.Border { Background = coverBrush };
                panel.Children.Add(cover);

                var shade = new System.Windows.Controls.Border
                {
                    VerticalAlignment = VerticalAlignment.Bottom, Height = 40,
                    Background = new System.Windows.Media.LinearGradientBrush(
                        Color.FromArgb(0, 0, 0, 0), Color.FromArgb(0xD0, 0, 0, 0), 90),
                };
                panel.Children.Add(shade);

                var name = new TextBlock
                {
                    Text = g.Name, Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxHeight = 30, Margin = new Thickness(8, 0, 8, 6),
                    VerticalAlignment = VerticalAlignment.Bottom,
                };
                panel.Children.Add(name);

                // 左上角 FSR 角标（AMD 红底 + 星标），仅当检测到 FidelityFX 运行时时显示
                var fsrHits = FsrDetector.ScanCached(g.WorkDir);
                if (fsrHits.Count > 0)
                {
                    string ver = fsrHits.Select(h => h.Version).FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? "";
                    var logoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "amd-logo.png");
                    System.Windows.Media.Brush badgeBg = System.IO.File.Exists(logoPath)
                        ? new System.Windows.Media.ImageBrush(LoadBitmap(logoPath)) { Stretch = Stretch.Uniform }
                        : new SolidColorBrush(Color.FromRgb(0xD8, 0x1A, 0x1A));
                    var badge = new Border
                    {
                        Background = badgeBg,
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(6, 2, 6, 2),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, 6, 6, 0),
                        ToolTip = "已检测到 AMD FidelityFX 运行时：" + string.Join("，", fsrHits.Select(h => $"{h.Kind} {h.Version}")),
                    };
                    var row = new StackPanel { Orientation = Orientation.Horizontal };
                    row.Children.Add(new TextBlock
                    {
                        Text = "★", Foreground = Brushes.White, FontSize = 10, Margin = new Thickness(0, 0, 3, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                    });
                    row.Children.Add(new TextBlock
                    {
                        Text = "FSR " + ver, Foreground = Brushes.White, FontSize = 10, FontWeight = FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center,
                    });
                    badge.Child = row;
                    panel.Children.Add(badge);
                }

                // 右键菜单：设置封面
                var cardMenu = new ContextMenu();
                var miCover = new MenuItem { Header = "设置封面…" };
                miCover.Click += (_, __) => PickCoverFor(g.ExePath);
                cardMenu.Items.Add(miCover);
                panel.ContextMenu = cardMenu;
                panel.Tag = g.ExePath;

                var cachedCover = GameLibrary.CoverFile(g);
                if (cachedCover != null && System.IO.File.Exists(cachedCover)) coverBrush.ImageSource = LoadBitmap(cachedCover);
                else
                {
                    // 无封面时用首字母色块，避免把小图标拉大
                    var initial = new TextBlock
                    {
                        Text = (g.Name.Length > 0 ? g.Name.Substring(0, 1) : "?").ToUpperInvariant(),
                        Foreground = new SolidColorBrush(Color.FromRgb(0x63, 0x63, 0x66)),
                        FontSize = 34, FontWeight = FontWeights.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    panel.Children.Insert(0, initial);
                    coverBrush.ImageSource = null;
                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        var path = await GameLibrary.EnsureCoverAsync(g);
                        if (path == null) return;
                        await Dispatcher.InvokeAsync(() => coverBrush.ImageSource = LoadBitmap(path));
                    });
                }
                card.Content = panel;
                var captured = g;
                card.Click += (_, __) => SelectGame(captured);
                GameGrid.Children.Add(card);
            }

            var add = new Button
            {
                Width = 150, Height = 86, Margin = new Thickness(6),
                Background = Brushes.Transparent, BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
                BorderThickness = new Thickness(1), Foreground = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xA6)),
                Content = new TextBlock { Text = "+ 添加游戏", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            };
            add.Click += (_, __) =>
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "选择游戏文件夹" };
                if (dlg.ShowDialog() != true) return;
                var g = GameLibrary.AddFolder(dlg.FolderName);
                StatusText.Text = g == null ? "该文件夹内没有找到 exe" : $"已添加：{g.Name}";
                BuildGameGrid(GameSearch.Text ?? "");
            };
            GameGrid.Children.Add(add);
                    Loc.Apply(this);
        }

        private void SelectGame(Game g)
        {
            if (CoverBtn != null) CoverBtn.Visibility = Visibility.Visible;
            if (AutoCoverBtn != null) AutoCoverBtn.Visibility = Visibility.Visible;
            _coverGameExe = g?.ExePath;
            InitCoverButton();
            SetMonitorVisible(false);
            ResetBtn.Visibility = Visibility.Visible;
            ResetBtn.Content = "重置此游戏";
            HighlightScope("games");
            _scopeGame = g.ExePath;
            SectionTitle.Text = "游戏 › " + g.Name;
            GamesPanel.Visibility = Visibility.Collapsed;
            GraphicsPanel.Visibility = Visibility.Visible;
            OcPanel.Visibility = Visibility.Collapsed;
            DisplayPanel.Visibility = Visibility.Collapsed;
            ProfilesPanel.Visibility = Visibility.Collapsed;
            BuildRows();
            RefreshScopeValues();
            RefreshProfileList();
        }

        private void BuildScopeRow()
        {
            _games = GameLibrary.Load();
            var grid = new Grid { Height = 48 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(new Border
            {
                Height = 1, VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2C)),
                Margin = new Thickness(16, 0, 0, 0)
            });
            grid.Children.Add(new TextBlock
            {
                Text = "作用域", Foreground = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xEA)),
                FontSize = 13, Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center
            });

            var combo = new ComboBox
            {
                Width = 220, Height = 28, Margin = new Thickness(0, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xEA)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x35)),
            };
            combo.Items.Add("全局");
            foreach (var g in _games) combo.Items.Add(g.Name);
            combo.SelectedIndex = 0;
            combo.SelectionChanged += (_, __) =>
            {
                if (_syncingScope) return;
                _scopeGame = combo.SelectedIndex <= 0 ? null : _games[combo.SelectedIndex - 1].ExePath;
                RefreshScopeValues();
            };
            Grid.SetColumn(combo, 1);
            grid.Children.Add(combo);
            RowsHost.Children.Add(grid);
        }

        private Profile ScopeProfile(string exePath, bool create)
        {
            var profiles = Engine.LoadProfiles();
            var key = "game:" + exePath;
            if (profiles.TryGetValue(key, out var p)) return p;
            if (!create) return null;
            var g = _games.FirstOrDefault(x => string.Equals(x.ExePath, exePath, StringComparison.OrdinalIgnoreCase));
            p = new Profile { Name = g?.Name ?? System.IO.Path.GetFileNameWithoutExtension(exePath), Process = exePath };
            profiles[key] = p;
            Engine.SaveProfiles(profiles);
            return p;
        }

        private void RefreshScopeValues()
        {
            if (_scopeGame == null)
            {
                foreach (var (key, _) in Items)
                {
                    bool on = RtCore.rt_toggle_get(key) == 1;
                    _state[key] = on;
                    Paint(key, on);
                }
                StatusText.Text = "作用域：全局（修改立即生效）";
                return;
            }
            var p = ScopeProfile(_scopeGame, false);
            foreach (var (key, _) in Items)
            {
                // 未覆盖时继承全局硬件值
                bool on = p != null && p.Toggles.TryGetValue(key, out var v) ? v == 1 : RtCore.rt_toggle_get(key) == 1;
                _state[key] = on;
                Paint(key, on);
            }
            StatusText.Text = $"作用域：{_games.FirstOrDefault(x => x.ExePath == _scopeGame)?.Name ?? "游戏"}（仅修改该配置）";
        }

        private void Paint(string key, bool on)
        {
            if (!_tracks.TryGetValue(key, out var track) || !_knobs.TryGetValue(key, out var knob)) return;
            track.Background = new SolidColorBrush(on
                ? Color.FromRgb(0x30, 0xD1, 0x58)
                : Color.FromRgb(0x48, 0x48, 0x4A));
            knob.HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            knob.Margin = on ? new Thickness(0, 0, 3, 0) : new Thickness(3, 0, 0, 0);
        }

        private async void Load()
        {
            var err = await Task.Run(() => RtCore.rt_init() == 0 ? null : RtCore.LastError());
            if (err != null)
            {
                StatusText.Text = "ADLX 初始化失败：" + err;
                var answer = MessageBox.Show(
                    "无法初始化 ADLX（" + err + "）。\n\n这通常需要管理员权限。\n是否以管理员身份重新启动？",
                    "RDNA Panel", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (answer == MessageBoxResult.Yes) RelaunchElevated();
                return;
            }

            var (values, gpu, disp) = await Task.Run(() =>
            {
                var d = new Dictionary<string, int>();
                foreach (var (key, _) in Items) d[key] = RtCore.rt_toggle_get(key);
                return (d, RtCore.GpuInfo(), RtCore.DisplayInfo());
            });

            foreach (var (key, _) in Items)
            {
                bool on = values.TryGetValue(key, out var v) && v == 1;
                _state[key] = on;
                Paint(key, on);
            }

            var oc = await Task.Run(() =>
            {
                var sb = new StringBuilder(256);
                int rc = RtCore.rt_oc2_info(sb, sb.Capacity);
                return (rc, sb.ToString());
            });
            var volt = await Task.Run(() =>
            {
                var sb = new StringBuilder(256);
                int rc = RtCore.rt_oc_info(sb, sb.Capacity);
                return (rc, sb.ToString());
            });

            var g = gpu.Split('|');
            var dparts = disp.Split('|');
            string disptxt = disp;
            if (dparts.Length == 3)
            {
                string bitsTxt = int.TryParse(dparts[1], out var bp) ? bp switch { 1 => "6", 2 => "8", 3 => "10", 4 => "12", 5 => "14", 6 => "16", _ => "?" } : "?";
                disptxt = $"{dparts[0]} · {bitsTxt}-bit";
            }
            // 先探测驱动语义（绝对值 / 偏移、是否支持最低频率），首页与超频页都依赖它
            ReadOcCaps();
            if (ClockCaption != null) ClockCaption.Text = Loc.T(_freqAbsolute ? "核心频率上限" : "核心频率");
            if (VoltCaption != null) VoltCaption.Text = Loc.T(_voltAbsolute ? "GPU 电压" : "电压偏移");
            // 侧栏不再显示设备信息（保留控件占位，避免其它引用失效）
            if (oc.Item1 == 0)
            {
                var f = oc.Item2.Split('|');
                if (f.Length >= 6)
                {
                    int off = int.Parse(f[2]);
                    if (_freqAbsolute)
                    {
                        ClockText.Text = off.ToString();            // RDNA2：ADLX 回读就是 MHz 绝对值
                        ClockSubText.Text = "频率上限";
                    }
                    else
                    {
                        ClockText.Text = (Rdna4BaseClock + off).ToString();
                        ClockSubText.Text = $"offset {off:+#;-#;0}";
                    }
                    PowerText.Text = $"{int.Parse(f[5]):+0;-0;0}%";
                }
            }
            if (volt.Item1 == 0)
            {
                var vf = volt.Item2.Split('|');
                if (vf.Length >= 13)
            {
                int off = int.Parse(vf[11]);
                VoltBigText.Text = off.ToString();   // RDNA4 负压显示为 -135；RDNA2 为绝对 mV
            }
            }
            StatusText.Text = "";
            if (_scopeGame != null) RefreshScopeValues();   // 游戏作用域下用配置值覆盖硬件值

            if (oc.Item1 == 0)
            {
                var f = oc.Item2.Split('|');
                if (f.Length == 6)
                {
                    _freqMin = int.Parse(f[0]); _freqMax = int.Parse(f[1]);
                    _powerMin = int.Parse(f[3]); _powerMax = int.Parse(f[4]);
                    _freqSlider.Minimum = _freqMin; _freqSlider.Maximum = _freqMax;
                    _powerSlider.Minimum = _powerMin; _powerSlider.Maximum = _powerMax;
                    _freqSlider.Value = int.Parse(f[2]);
                    _powerSlider.Value = int.Parse(f[5]);
                    var mb = new StringBuilder(64);
                    if (_minFreqSupported && _minFreqSlider != null &&
                        RtCore.rt_oc2_min(mb, mb.Capacity) == 0)
                    {
                        var mv = mb.ToString().Split('|');
                        if (mv.Length >= 3)
                        {
                            _minFreqSlider.Minimum = int.Parse(mv[0]);
                            _minFreqSlider.Maximum = int.Parse(mv[1]);
                            _minFreqSlider.Value = int.Parse(mv[2]);
                        }
                    }
                    var vb = new StringBuilder(64);
                    if (RtCore.rt_oc2_volt(vb, vb.Capacity) == 0)
                    {
                        var vv = vb.ToString().Split('|');
                        if (vv.Length >= 3)
                        {
                            _voltSlider.Minimum = int.Parse(vv[0]);
                            _voltSlider.Maximum = int.Parse(vv[1]);
                            _voltSlider.Value = int.Parse(vv[2]);
                            _voltText.Text = $"ADLX 范围 {vv[0]} … {vv[1]} mV";
                        }
                    }
                    UpdateOcText();
                }
            }
        }

        private async Task ToggleAsync(string key)
        {
            bool want = !_state[key];

            if (_scopeGame != null)
            {
                var p = ScopeProfile(_scopeGame, true);
                p.Toggles[key] = want ? 1 : 0;
                var profiles = Engine.LoadProfiles();
                profiles["game:" + _scopeGame] = p;
                Engine.SaveProfiles(profiles);
                _state[key] = want;
                Paint(key, want);
                StatusText.Text = $"{p.Name}：{key} = {(want ? "ON" : "OFF")}（已保存，进入该游戏时生效）";
                return;
            }

            StatusText.Text = $"正在写入 {key}…";
            var prof = new Profile { Name = "toggle:" + key, HasOc = false };
            prof.Toggles[key] = want ? 1 : 0;

            var r = await Task.Run(() => Engine.Apply(prof));
            int got = await Task.Run(() => RtCore.rt_toggle_get(key));
            _state[key] = got == 1;
            Paint(key, _state[key]);

            StatusText.Text = r.Ok
                ? $"{key} → {(got == 1 ? "ON" : "OFF")}（回读一致）"
                : $"{key} 写入失败：{r.Detail}";
        }
    }
}
