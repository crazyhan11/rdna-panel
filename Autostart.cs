using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace RadeonTune
{
    /// <summary>登录自启：写 HKCU Run 键（无需管理员），延迟可在界面里自由设置。</summary>
    public static class Autostart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "RDNA Panel";
        private const string TaskName = "RDNAPanel-AutoApply";
        private static string SettingsPath => Path.Combine(Engine.StoreDir, "settings.json");

        public class Settings
        {
            public bool Enabled { get; set; }
            public int DelaySeconds { get; set; } = 30;
            /// <summary>关闭按钮行为：ask=每次询问，tray=最小化到托盘，exit=退出程序。</summary>
            public string CloseAction { get; set; } = "ask";
            /// <summary>界面语言：zh / en。</summary>
            public string Language { get; set; } = "zh";
            /// <summary>选中的显卡索引（多卡时使用）。</summary>
            public int GpuIndex { get; set; } = -1;
            /// <summary>当前操作的显示器索引（多屏）。</summary>
            public int DisplayIndex { get; set; } = 0;
            /// <summary>是否同时轮询非当前显卡（会阻止 CPU 进入深 C-state，默认关闭）。</summary>
            public bool PollOtherGpus { get; set; } = false;
        }

        public static Settings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? new Settings();
            }
            catch { }
            return new Settings();
        }

        public static void Save(Settings s)
        {
            try
            {
                Directory.CreateDirectory(Engine.StoreDir);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public static bool IsInstalled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) != null;
            }
            catch { return false; }
        }

        public static string Install(int delaySeconds)
        {
            if (delaySeconds < 0) delaySeconds = 0;
            if (delaySeconds > 3600) delaySeconds = 3600;
            var exe = Path.Combine(AppContext.BaseDirectory, "RDNAPanel.exe");
            if (!File.Exists(exe)) exe = Process.GetCurrentProcess().MainModule.FileName;
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                if (key == null) return "无法打开注册表 Run 键";
                key.SetValue(ValueName, $"\"{exe}\" --minimized --delay {delaySeconds}");
            }
            catch (Exception ex) { return "写入注册表失败：" + ex.Message; }

            RemoveLegacyTask();
            var s = Load();
            s.Enabled = true;
            s.DelaySeconds = delaySeconds;
            Save(s);
            return $"已启用：登录后 {delaySeconds} 秒启动并最小化到托盘";
        }

        public static string Uninstall()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            catch { }
            RemoveLegacyTask();
            var s = Load();
            s.Enabled = false;
            Save(s);
            return "已关闭登录自启";
        }

        private static void RemoveLegacyTask()
        {
            try
            {
                var script = $"Unregister-ScheduledTask -TaskName '{TaskName}' -Confirm:$false -ErrorAction SilentlyContinue";
                var enc = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
                var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {enc}")
                { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psi)?.WaitForExit(10000);
            }
            catch { }
        }
    }
}
