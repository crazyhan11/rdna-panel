using System;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace RadeonTune
{
    public partial class App : Application
    {
        private static readonly string FlagDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RDNA Panel");
        private static readonly string BootFlag = Path.Combine(FlagDir, "starting.flag");
        private static readonly string SafeFlag = Path.Combine(FlagDir, "safe.flag");

        internal static bool SoftwareRendering { get; private set; }
        internal static string StartPage { get; private set; } = "graphics";
        internal static string ShotPath { get; private set; }
        internal static string ScopeArg { get; private set; }
        internal static string DisplayPage { get; private set; } = "basic";
        internal static bool OpenDropdown { get; private set; }
        internal static int ShotWidth { get; private set; }
        internal static int ShotHeight { get; private set; }

        private static System.Threading.Mutex _single;

        public static bool StartMinimized = false;


        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr FindWindowW(string cls, string title);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr h, int cmd);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr h);
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern bool PostMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);
        public const uint WmShowWindow = 0x8000 + 1;   // 自定义：唤醒

        private const string ShowEventName = "RDNAPanel.ShowRequest";

        /// <summary>已有实例：通过命名事件请求它把窗口显示出来。</summary>
        public static void ActivateRunningInstance()
        {
            try
            {
                using var ev = System.Threading.EventWaitHandle.OpenExisting(ShowEventName);
                ev.Set();
            }
            catch { }
        }

        /// <summary>首个实例：监听唤醒事件（窗口被隐藏时 HWND 会被销毁，不能用 FindWindow）。</summary>
        private static void StartShowListener()
        {
            try
            {
                var ev = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, ShowEventName);
                var th = new System.Threading.Thread(() =>
                {
                    while (true)
                    {
                        ev.WaitOne();
                        try
                        {
                            Current?.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try { TrayHost.ShowWindow(); } catch { }
                            }));
                        }
                        catch { }
                    }
                })
                { IsBackground = true };
                th.Start();
            }
            catch { }
        }

        private static System.Threading.Mutex _instanceMutex;

        private static bool IsCliMode(string[] args)
        {
            // 只有“一次性命令”允许与主程序并存；--minimized 属于主程序本身，仍受单实例限制
            string[] oneShot = { "--shot", "--applyonly", "--autostart", "--rawsetres", "--setres",
                                 "--selftest", "--octest", "--presettest", "--covers", "--rescan",
                                 "--edidtest", "--fsrtest", "--vfsweep", "--vftest", "--vfsample" };
            foreach (var a in args)
                foreach (var o in oneShot)
                    if (string.Equals(a, o, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool EnsureSingleInstance(string[] args)
        {
            if (IsCliMode(args)) return true;
            _instanceMutex = new System.Threading.Mutex(true, "RDNAPanel.SingleInstance", out bool created);
            if (created) return true;
            // 已有一个实例：静默退出。
            // 若是“最小化启动”（开机自启）则绝不去恢复/前置已有窗口，避免开机时弹窗；
            // 只有用户手动双击启动时才把已有窗口拉到前台。
            bool minimizedStart = Array.IndexOf(args, "--minimized") >= 0;
            if (!minimizedStart) ActivateRunningInstance();
            return false;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            {
                if (Array.IndexOf(e.Args, "--applyonly") >= 0)
                {
                    try
                    {
                        var auto = Engine.LoadProfiles().Values.FirstOrDefault(x => x.AutoApply && x.HasOc);
                        string msg;
                        if (auto == null) msg = "no auto-apply preset";
                        else
                        {
                            var r = Engine.ApplySafe(auto);
                            msg = r.Ok ? $"applied {auto.Name} (freq {auto.FreqOffset} volt {auto.VoltMv} pwr {auto.PowerPct})" : $"failed {auto.Name}: {r.Detail}";
                        }
                        System.IO.Directory.CreateDirectory(Engine.StoreDir);
                        System.IO.File.AppendAllText(System.IO.Path.Combine(Engine.StoreDir, "autostart.log"),
                            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}{Environment.NewLine}");
                    }
                    catch (Exception ex)
                    {
                        try { System.IO.File.AppendAllText(System.IO.Path.Combine(Engine.StoreDir, "autostart.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} EX {ex.Message}{Environment.NewLine}"); } catch { }
                    }
                    Environment.Exit(0);
                }
            }
            {
                var ri = Array.FindIndex(e.Args, x => string.Equals(x, "--rawsetres", StringComparison.OrdinalIgnoreCase));
                if (ri >= 0 && ri + 3 < e.Args.Length)
                {
                    int rw = int.Parse(e.Args[ri + 1]), rh = int.Parse(e.Args[ri + 2]), rz = int.Parse(e.Args[ri + 3]);
                    int rc1 = RtCore.rt_set_mode(rw, rh, rz);
                    var mb2 = new System.Text.StringBuilder(64);
                    RtCore.rt_mode(mb2, mb2.Capacity);
                    var id = System.Security.Principal.WindowsIdentity.GetCurrent();
                    var pr = new System.Security.Principal.WindowsPrincipal(id);
                    System.IO.File.WriteAllText("rawsetres.txt",
                        $"set={rc1} now={mb2} user={id.Name} admin={pr.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator)}");
                    Environment.Exit(0);
                }
            }
            foreach (var a in e.Args)
            {
                if (string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase))
                {
                    RunCli("selftest.txt", Engine.SelfTest);
                    Environment.Exit(0);
                }
                if (string.Equals(a, "--octest", StringComparison.OrdinalIgnoreCase))
                {
                    RunCli("octest.txt", Engine.OcRoundTripTest);
                    Environment.Exit(0);
                }
                if (string.Equals(a, "--size", StringComparison.OrdinalIgnoreCase) && e.Args.Length > 1)
                {
                    var parts = e.Args[1].Split('x');
                    if (parts.Length == 2 && int.TryParse(parts[0], out var sw) && int.TryParse(parts[1], out var sh))
                    {
                        ShotWidth = sw; ShotHeight = sh;
                    }
                }
                if (string.Equals(a, "--shot", StringComparison.OrdinalIgnoreCase))
                {
                    ShotPath = e.Args.Length > 1 ? e.Args[1] : "shot.png";
                }
                if (string.Equals(a, "--procmatch", StringComparison.OrdinalIgnoreCase))
                {
                    var target = e.Args.Length > 1 ? e.Args[1] : "";
                    RunCli("procmatch.txt", () =>
                    {
                        var p = Engine.FindProfileForProcess(target);
                        return p == null ? $"no match for '{target}'" : $"match '{target}' -> {p.Name} (freq {p.FreqOffset}, volt {p.VoltMv}, toggles {p.Toggles.Count})";
                    });
                    Environment.Exit(0);
                }
                if (string.Equals(a, "--presettest", StringComparison.OrdinalIgnoreCase))
                {
                    RunCli("presettest.txt", () =>
                    {
                        var sb2 = new System.Text.StringBuilder();
                        void Line(string s2) => sb2.AppendLine(s2);
                        var before = Engine.CaptureHw();
                        Line($"before: freq {before.FreqOffset:+#;-#;0} volt {before.VoltMv} power {before.PowerPct:+#;-#;0} fan {before.FanPct}%");

                        var fb = new System.Text.StringBuilder(256);
                        string curve = "";
                        if (RtCore.rt_fan_curve(fb, fb.Capacity) == 0) curve = fb.ToString();
                        Line($"fan curve: {curve}");

                        var all = Engine.LoadProfiles();
                        var testName = "__selfcheck";
                        all[testName] = new Profile
                        {
                            Name = testName, HasOc = true,
                            FreqOffset = before.FreqOffset, VoltMv = before.VoltMv, PowerPct = before.PowerPct,
                            FanPct = before.FanPct, FanCurve = curve,
                        };
                        Engine.SaveProfiles(all);

                        var reloaded = Engine.LoadProfiles().TryGetValue(testName, out var pr) ? pr : null;
                        Line($"reload: {(reloaded == null ? "FAIL(not found)" : $"name={reloaded.Name} freq={reloaded.FreqOffset} volt={reloaded.VoltMv} power={reloaded.PowerPct} curveLen={reloaded.FanCurve.Length}")}");

                        bool ok = true;
                        if (reloaded == null) ok = false;
                        else
                        {
                            ok &= reloaded.FreqOffset == before.FreqOffset;
                            ok &= reloaded.VoltMv == before.VoltMv;
                            ok &= reloaded.PowerPct == before.PowerPct;
                            ok &= reloaded.FanCurve == curve;
                            Line($"fields match: {ok}");
                        }

                        var applyRes = Engine.ApplySafe(reloaded);
                        Line($"apply: {applyRes.Ok} {applyRes.Detail}");
                        ok &= applyRes.Ok;

                        var after = Engine.CaptureHw();
                        Line($"after: freq {after.FreqOffset:+#;-#;0} volt {after.VoltMv} power {after.PowerPct:+#;-#;0} fan {after.FanPct}%");
                        bool same = after.FreqOffset == before.FreqOffset && after.VoltMv == before.VoltMv && after.PowerPct == before.PowerPct;
                        Line($"roundtrip: {(same ? "OK" : "MISMATCH")}");
                        ok &= same;

                        var restore = Engine.RestoreRollback();
                        Line($"restore: {restore}");

                        all = Engine.LoadProfiles();
                        all.Remove(testName);
                        Engine.SaveProfiles(all);
                        Line($"cleanup: removed {testName}");
                        Line(ok ? "RESULT: PASS" : "RESULT: FAIL");
                        return sb2.ToString();
                    });
                    Environment.Exit(0);
                }
                if (string.Equals(a, "--covers", StringComparison.OrdinalIgnoreCase))
                {
                    RunCli("covers.txt", () =>
                    {
                        var sb2 = new System.Text.StringBuilder();

                        var allG = GameLibrary.Load();
                        bool migrated = false;
                        foreach (var gg in allG)
                        {
                            if (string.IsNullOrEmpty(gg.CustomCover) || !System.IO.File.Exists(gg.CustomCover)) continue;
                            var covDir = System.IO.Path.Combine(Engine.StoreDir, "covers");
                            if (gg.CustomCover.StartsWith(covDir, StringComparison.OrdinalIgnoreCase)) continue;
                            var cp = GameLibrary.SaveCoverCopy(gg.CustomCover, gg.ExePath);
                            sb2.AppendLine($"{gg.Name}: migrated cover -> {cp}");
                            gg.CustomCover = cp;
                            migrated = true;
                        }
                        if (migrated) GameLibrary.Save(allG);
                        var allGames = GameLibrary.Load();
                        foreach (var g in allGames)
                        {
                            var pth = GameLibrary.CoverFile(g);
                            if (pth == null || !System.IO.File.Exists(pth))
                                pth = System.Threading.Tasks.Task.Run(() => GameLibrary.EnsureCoverAsync(g)).GetAwaiter().GetResult();
                            if (pth == null && g.AppId != null)
                                pth = GameLibrary.EnsureManualCover(g);
                            long sz = pth != null && System.IO.File.Exists(pth) ? new System.IO.FileInfo(pth).Length : 0;
                            sb2.AppendLine($"{g.Name}: {(pth == null ? "none" : pth)} ({sz} bytes)");
                        }
                        GameLibrary.Save(allGames);
                        return sb2.ToString();
                    });
                    Environment.Exit(0);
                }
                if (string.Equals(a, "--rescan", StringComparison.OrdinalIgnoreCase))
                {
                    RunCli("rescan.txt", () =>
                    {
                        var sb2 = new System.Text.StringBuilder();
                        var games = GameLibrary.Load();
                        foreach (var g in games)
                        {
                            if (!string.Equals(g.Source, "manual", StringComparison.OrdinalIgnoreCase)) continue;
                            var dir = g.WorkDir;
                            if (string.IsNullOrEmpty(dir) || !System.IO.Directory.Exists(dir)) continue;
                            var better = GameLibrary.FindBestExe(dir, 3);
                            if (better != null && !string.Equals(better, g.ExePath, StringComparison.OrdinalIgnoreCase))
                            {
                                sb2.AppendLine($"{g.Name}: {g.ExePath} -> {better}");
                                g.ExePath = better;
                                g.WorkDir = System.IO.Path.GetDirectoryName(better);
                            }
                            var cover = GameLibrary.EnsureManualCover(g);
                            sb2.AppendLine($"  cover: {cover ?? "none"}");
                        }
                        GameLibrary.Save(games);
                        return sb2.ToString();
                    });
                    Environment.Exit(0);
                }
                if (string.Equals(a, "--fsrtest", StringComparison.OrdinalIgnoreCase))
                {
                    RunCli("fsrtest.txt", () =>
                    {
                        var sb = new System.Text.StringBuilder();
                        var allGames = GameLibrary.Load();
                        foreach (var g in allGames)
                        {
                            var hits = FsrDetector.Scan(g.WorkDir);
                            sb.AppendLine($"[{g.Name}] dir={g.WorkDir}");
                            if (hits.Count == 0) sb.AppendLine("  未找到 AMD FidelityFX DLL");
                            foreach (var h in hits) sb.AppendLine($"  {h.Kind}: {h.File}  → FSR {h.Version}");
                        }
                        return sb.ToString();
                    });
                    Environment.Exit(0);
                }
                if (string.Equals(a, "--crtest2", StringComparison.OrdinalIgnoreCase))
                {
                    RunCli("crtest2.txt", () =>
                    {
                        var cur = new System.Text.StringBuilder(512);
                        if (RtCore.rt_cr_current(cur, cur.Capacity) != 0) return "read failed";
                        var f = cur.ToString().Split(',');
                        var e = new System.Text.StringBuilder(8192);
                        RtCore.rt_edid(e, e.Capacity);
                        var info = EdidInfo.Parse(e.ToString());
                        var tmpl = new EdidTiming
                        {
                            HActive = int.Parse(f[0]), VActive = int.Parse(f[1]),
                            HBlank = int.Parse(f[7]) - int.Parse(f[0]), VBlank = int.Parse(f[8]) - int.Parse(f[1]),
                            HFront = int.Parse(f[11]), VFront = int.Parse(f[12]),
                            HSync = int.Parse(f[13]), VSync = int.Parse(f[14]),
                        };
                        int w = 2560, h = 1440, hz = 60;
                        var sb = new System.Text.StringBuilder();
                        foreach (int std in new[] { 0, 1, 2, 4 })
                        {
                            var t = EdidInfo.Compute(w, h, hz, std, tmpl);
                            int rc = RtCore.rt_cr_add_ex(w, h, hz, 0, std, t.PixelClockKHz, 0,
                                t.HTotal, t.VTotal, w, h, t.HFront, t.VFront, t.HSync, t.VSync,
                                t.HNegative ? 1 : 0, t.VNegative ? 1 : 0);
                            var list = new System.Text.StringBuilder(4096);
                            int n = RtCore.rt_customres(list, list.Capacity);
                            if (n > 0) RtCore.rt_cr_del(0);
                            sb.AppendLine($"std={std} pclk={t.PixelClockKHz} hTot={t.HTotal} vTot={t.VTotal} create_rc={rc} count={n}");
                        }
                        return sb.ToString();
                    });
                    Environment.Exit(0);
                }
                if (string.Equals(a, "--edidtest", StringComparison.OrdinalIgnoreCase))
                {
                    RunCli("edidtest.txt", () =>
                    {
                        var b = new System.Text.StringBuilder(8192);
                        if (RtCore.rt_edid(b, b.Capacity) != 0) return "no edid";
                        var info = EdidInfo.Parse(b.ToString());
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine($"name={info.MonitorName} range={info.MinVHz}-{info.MaxVHz}Hz maxPixClk={info.MaxPixelClockMHz}MHz");
                        foreach (var t in info.Timings)
                            sb.AppendLine($"DTD {t.HActive}x{t.VActive}@{t.Refresh:F2} pclk={t.PixelClockKHz} hTot={t.HTotal} vTot={t.VTotal}");
                        var mb = new System.Text.StringBuilder(16384);
                        RtCore.rt_modes(mb, mb.Capacity);
                        int MaxFor(int w2, int h2)
                        {
                            int best = 0;
                            foreach (var e in mb.ToString().Split(';', StringSplitOptions.RemoveEmptyEntries))
                            {
                                var p2 = e.Split(',');
                                if (p2.Length == 3 && int.Parse(p2[0]) == w2 && int.Parse(p2[1]) == h2) best = Math.Max(best, int.Parse(p2[2]));
                            }
                            return best;
                        }
                        foreach (var (w, h, hz) in new[] { (2560, 1440, 60), (2560, 1440, 320), (2560, 1440, 400), (1920, 1080, 75), (3840, 2160, 60) })
                        {
                            var t = EdidInfo.ComputeCvtRb(w, h, hz);
                            var probs = info.Validate(w, h, hz, t, MaxFor(w, h));
                            sb.AppendLine($"{w}x{h}@{hz} pclk={t.PixelClockKHz} -> {(probs.Count == 0 ? "accept" : "REJECT: " + string.Join("; ", probs))}");
                        }
                        return sb.ToString();
                    });
                    Environment.Exit(0);
                }
                if (string.Equals(a, "--crtest", StringComparison.OrdinalIgnoreCase))
                {
                    RunCli("crtest.txt", () =>
                    {
                        var b = new System.Text.StringBuilder(512);
                        if (RtCore.rt_cr_current(b, b.Capacity) != 0) return "read current failed";
                        var f = b.ToString().Split(',');
                        int w = int.Parse(f[0]), h = int.Parse(f[1]), hz = int.Parse(f[2]);
                        int rc = RtCore.rt_cr_add_ex(w, h, hz, int.Parse(f[3]), int.Parse(f[4]), int.Parse(f[5]),
                            int.Parse(f[6]), int.Parse(f[7]), int.Parse(f[8]), int.Parse(f[9]), int.Parse(f[10]),
                            int.Parse(f[11]), int.Parse(f[12]), int.Parse(f[13]), int.Parse(f[14]),
                            int.Parse(f[15]), int.Parse(f[16]));
                        var after = new System.Text.StringBuilder(4096);
                        int n = RtCore.rt_customres(after, after.Capacity);
                        string list = after.ToString();
                        int del = n > 0 ? RtCore.rt_cr_del(0) : -99;
                        var back = new System.Text.StringBuilder(4096);
                        int n2 = RtCore.rt_customres(back, back.Capacity);
                        return $"create rc={rc} count={n} [{list}] delete rc={del} count_after={n2}";
                    });
                    Environment.Exit(0);
                }
                if (string.Equals(a, "--gametest", StringComparison.OrdinalIgnoreCase))
                {
                    var exe = e.Args.Length > 1 ? e.Args[1] : @"C:\test\game.exe";
                    RunCli("gametest.txt", () =>
                    {
                        int before = RtCore.rt_toggle_get("chill");
                        var profiles = Engine.LoadProfiles();
                        var key = "game:" + exe;
                        if (!profiles.TryGetValue(key, out var p)) { p = new Profile { Name = "gametest", Process = exe }; profiles[key] = p; }
                        p.Toggles["chill"] = 1;
                        Engine.SaveProfiles(profiles);
                        var again = Engine.LoadProfiles();
                        bool stored = again.TryGetValue(key, out var p2) && p2.Toggles.TryGetValue("chill", out var v) && v == 1;
                        int after = RtCore.rt_toggle_get("chill");
                        return $"scope=game:{exe}\nstored chill=1 -> {stored}\nhardware chill before={before} after={after} (unchanged={before == after})";
                    });
                    Environment.Exit(0);
                }
                if (string.Equals(a, "--games", StringComparison.OrdinalIgnoreCase))
                {
                    RunCli("games.txt", () =>
                    {
                        var games = GameLibrary.Scan();
                        var lines = games.Take(30).Select(g => $"{g.Source}\t{g.Name}\t{g.ExePath}");
                        return string.Join(Environment.NewLine, lines) + $"{Environment.NewLine}count={games.Count}";
                    });
                    Environment.Exit(0);
                }
                if (string.Equals(a, "--gate", StringComparison.OrdinalIgnoreCase))
                {
                    RunCli("gate.txt", () => { var g = Engine.BootGate(); return g.Action + " " + g.Detail; });
                    Environment.Exit(0);
                }
            }

            // 去掉全局互斥量：非管理员下容易创建失败/被僵尸实例占住，导致静默退出
            try { StartupCore(e); }
            catch (Exception ex) { Fatal("OnStartup", ex); Shutdown(1); }
        }

        private void StartupCore(StartupEventArgs e)
        {
            Log("stage=startup");
            if (!EnsureSingleInstance(e.Args)) { Log("another instance running; exiting"); Environment.Exit(0); }
            StartShowListener();
            try { Loc.Language = Autostart.Load().Language ?? "zh"; } catch { Loc.Language = "zh"; }
            try
            {
                var cfg0 = Autostart.Load();
                if (cfg0.GpuIndex >= 0 && RtCore.rt_init() == 0)
                {
                    int cnt = RtCore.rt_gpu_count();
                    if (cfg0.GpuIndex < cnt) RtCore.rt_gpu_select(cfg0.GpuIndex);
                }
            }
            catch { }
            if (Array.IndexOf(e.Args, "--minimized") >= 0) StartMinimized = true;
            {
                var ai = Array.IndexOf(e.Args, "--autostart");
                if (ai >= 0)
                {
                    string msg;
                    if (ai + 1 < e.Args.Length && int.TryParse(e.Args[ai + 1], out var secs))
                        msg = Autostart.Install(secs);
                    else
                        msg = Autostart.Uninstall();
                    System.IO.Directory.CreateDirectory(Engine.StoreDir);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(Engine.StoreDir, "autostart-result.txt"), msg);
                    Environment.Exit(0);
                }
            }

            bool safe = false;
            for (int i = 0; i < e.Args.Length; i++)
            {
                if (string.Equals(e.Args[i], "--page", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
                {
                    var pg = e.Args[i + 1].ToLowerInvariant();
                    StartPage = pg.StartsWith("display") ? "display" : pg;
                    if (pg.Contains(':')) DisplayPage = pg.Split(':')[1];
                }
                if (string.Equals(e.Args[i], "--scope", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
                    ScopeArg = e.Args[i + 1];
                if (string.Equals(e.Args[i], "--dropdown", StringComparison.OrdinalIgnoreCase))
                    OpenDropdown = true;
                if (string.Equals(e.Args[i], "--safe", StringComparison.OrdinalIgnoreCase))
                    safe = true;
            }

            SoftwareRendering = safe;
            Log($"start args=[{string.Join(" ", e.Args)}] safe={safe} renderMode={(safe ? "software" : "hardware")}");
            if (safe)
                RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

            Log("stage=creating-window");
            int startDelay = 0;
            {
                var di = Array.IndexOf(e.Args, "--delay");
                if (di >= 0 && di + 1 < e.Args.Length) int.TryParse(e.Args[di + 1], out startDelay);
                if (startDelay < 0) startDelay = 0;
            }

            void RunStartupApply()
            {
                try
                {
                    if (Engine.ApplyPending)
                    {
                        Log("startup: 检测到上次应用未完成，回退");
                        Engine.MarkPending(false);
                        Log("startup: " + Engine.RestoreRollback());
                    }
                    else
                    {
                        var auto = Engine.LoadProfiles().Values.FirstOrDefault(x => x.AutoApply);
                        if (auto != null)
                        {
                            var r = Engine.ApplySafe(auto);
                            Log($"startup: 开机应用 {auto.Name} -> {(r.Ok ? "OK" : r.Detail)}");
                            if (!r.Ok) Log("startup: " + Engine.RestoreRollback());
                        }
                    }
                }
                catch (Exception ex) { Log("startup apply failed: " + ex.Message); }
            }

            if (startDelay > 0)
            {
                Log($"startup: 延迟 {startDelay}s 后应用开机预设");
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(startDelay) };
                timer.Tick += (_, __) => { timer.Stop(); RunStartupApply(); };
                timer.Start();
            }
            else
            {
                RunStartupApply();
            }

            var window = new MainWindow();
            try
            {
                var idx = Array.FindIndex(e.Args, x => string.Equals(x, "--setres", StringComparison.OrdinalIgnoreCase));
                if (idx >= 0 && idx + 3 < e.Args.Length)
                {
                    int rw = int.Parse(e.Args[idx + 1]), rh = int.Parse(e.Args[idx + 2]), rz = int.Parse(e.Args[idx + 3]);
                    window.CliApplyMode(rw, rh, rz);
                    Shutdown(0);
                    return;
                }
            }
            catch { }
            MainWindow = window;
            if (StartMinimized)
            {
                TrayHost.Attach(window);   // 先建托盘图标，再隐藏窗口
                window.ShowInTaskbar = false;
                window.WindowState = WindowState.Minimized;
                window.Hide();
            }
            else
            {
                window.Show();
            }
            Log("stage=window-shown");
            StartTdrWatchdog();
            base.OnStartup(e);
        }

        internal static void RunCli(string fileName, Func<string> work)
        {
            string text;
            try { text = work(); }
            catch (Exception ex) { text = "ERROR: " + ex; }
            foreach (var dir in new[] { FlagDir, AppContext.BaseDirectory, Environment.CurrentDirectory })
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, fileName), text);
                    break;
                }
                catch { }
            }
        }

        private static void Log(string msg)
        {
            var line = DateTime.Now.ToString("s") + " " + msg + Environment.NewLine;
            foreach (var dir in new[] { FlagDir, AppContext.BaseDirectory, Environment.CurrentDirectory })
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    File.AppendAllText(Path.Combine(dir, "error.log"), line);
                    return;
                }
                catch { }
            }
        }

        internal static void Fatal(string where, Exception ex)
        {
            Log("FATAL " + where + ": " + ex);
            try
            {
                MessageBox.Show("RadeonTune 启动失败：\n\n" + ex.Message + "\n\n位置：" + where +
                                "\n\n日志：%LOCALAPPDATA%\\RadeonTune\\error.log 或 exe 目录下 error.log",
                                "RDNA Panel", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { File.Delete(BootFlag); } catch { }
            base.OnExit(e);
        }

        private static DateTime _lastTdrCheck = DateTime.Now;

        /// <summary>TDR 看门狗：检测到显示驱动重置（事件 4101）后重新应用开机预设。</summary>
        private static void StartTdrWatchdog()
        {
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            t.Tick += (_, __) =>
            {
                try
                {
                    var since = _lastTdrCheck;
                    _lastTdrCheck = DateTime.Now;
                    bool tdr = false;
                    using (var log = new System.Diagnostics.EventLog("System"))
                    {
                        var entries = log.Entries;
                        int start = Math.Max(0, entries.Count - 200);
                        for (int i = entries.Count - 1; i >= start; i--)
                        {
                            var e = entries[i];
                            if (e.TimeGenerated < since) break;
                            if (e.InstanceId == 4101 || e.InstanceId == 1 && (e.Source ?? "").Contains("amdkmdag"))
                            {
                                tdr = true;
                                break;
                            }
                        }
                    }
                    if (!tdr) return;
                    var auto = Engine.LoadProfiles().Values.FirstOrDefault(x => x.AutoApply);
                    if (auto == null) return;
                    var r = Engine.ApplySafe(auto);
                    Log($"tdr: recovered, reapply {auto.Name} -> {(r.Ok ? "OK" : r.Detail)}");
                }
                catch { }
            };
            t.Start();
            Log("tdr: watchdog started");
        }
    }
}
