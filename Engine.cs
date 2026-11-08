using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace RadeonTune
{
    public class Profile
    {
        public string Name { get; set; } = "default";
        public Dictionary<string, int> Toggles { get; set; } = new();
        public int FreqOffset { get; set; }
        public int MinFreq { get; set; }
        public int PowerPct { get; set; }
        public int VoltMv { get; set; }
        public string Process { get; set; } = "";
        public int FanPct { get; set; }
        public string FanCurve { get; set; } = "";
        public bool AutoApply { get; set; }
        public bool HasOc { get; set; }
    }

    public class History
    {
        public int Strikes { get; set; }
        public List<string> Quarantined { get; set; } = new();
        public long LastApply { get; set; }
    }

    public class EngineResult
    {
        public string Action { get; set; } = "none";
        public string Detail { get; set; } = "";
        public bool Ok { get; set; } = true;
    }

    public static class Engine
    {
        public static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RDNA Panel");
        public static readonly string StoreDir = ProbeStoreDir();
        public static readonly string StatePath = Path.Combine(StoreDir, "state.json");
        public static readonly string HistoryPath = Path.Combine(StoreDir, "history.json");
        public static readonly string ProfilesPath = Path.Combine(StoreDir, "profiles.json");

        // 不探测、不阻塞：固定使用 exe 同目录的 data 子目录，写入失败时由 Save 的 try/catch 兜住
        private static string ProbeStoreDir() => Path.Combine(AppContext.BaseDirectory, "data");

        public const int BreakerMax = 3;

        private static void EnsureDir()
        {
            try { Directory.CreateDirectory(StoreDir); } catch { }
        }
        public static T Load<T>(string path, T fallback) where T : class
        {
            try
            {
                if (File.Exists(path)) return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? fallback;
            }
            catch { }
            return fallback;
        }

        public static void Save<T>(string path, T value)
        {
            var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
            try { Directory.CreateDirectory(Path.GetDirectoryName(path)); } catch { }
            var tmp = path + ".tmp";
            try
            {
                File.WriteAllText(tmp, json);
                File.Move(tmp, path, true);
            }
            catch
            {
                try { File.Delete(tmp); } catch { }
                File.WriteAllText(path, json);
            }
        }

        public static Dictionary<string, Profile> LoadProfiles()
            => Load(ProfilesPath, new Dictionary<string, Profile>()) ?? new();

        public static void SaveProfiles(Dictionary<string, Profile> profiles) => Save(ProfilesPath, profiles);

        // ---------- 应用前快照 / 回退 ----------
        public class HwState
        {
            public string SavedAt { get; set; } = "";
            public int FreqOffset { get; set; }
            public int MinFreq { get; set; }
            public int VoltMv { get; set; }
            public int PowerPct { get; set; }
            public int FanPct { get; set; }
            public bool HasFan { get; set; }
            public bool HasMinFreq { get; set; }
        }

        private static string RollbackPath => Path.Combine(StoreDir, "rollback.json");
        private static string PendingPath => Path.Combine(StoreDir, "apply-pending.flag");

        public static HwState CaptureHw()
        {
            var st = new HwState { SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            var sb = new StringBuilder(128);
            if (RtCore.rt_oc2_info(sb, sb.Capacity) == 0)
            {
                var f = sb.ToString().Split('|');
                if (f.Length > 2 && int.TryParse(f[2], out var fo)) st.FreqOffset = fo;
                if (f.Length > 5 && int.TryParse(f[5], out var pw)) st.PowerPct = pw;
            }
            sb.Clear();
            if (RtCore.rt_oc2_volt(sb, sb.Capacity) == 0)
            {
                var v = sb.ToString().Split('|');
                if (v.Length > 2 && int.TryParse(v[2], out var mv)) st.VoltMv = mv;
            }
            sb.Clear();
            if (RtCore.rt_oc2_min(sb, sb.Capacity) == 0)
            {
                var m = sb.ToString().Split('|');
                if (m.Length > 2 && int.TryParse(m[2], out var mn)) { st.MinFreq = mn; st.HasMinFreq = true; }
            }
            sb.Clear();
            if (RtCore.rt_fan(sb, sb.Capacity) == 0)
            {
                var fa = sb.ToString().Split('|');
                if (fa.Length > 2 && int.TryParse(fa[2], out var fp)) { st.FanPct = fp; st.HasFan = true; }
            }
            return st;
        }

        public static void SaveRollback(HwState st) => Save(RollbackPath, st);
        public static HwState LoadRollback() => Load<HwState>(RollbackPath, null);

        public static string RestoreRollback()
        {
            var st = LoadRollback();
            if (st == null) return "没有可回退的快照";
            int ok = 0, fail = 0;
            if (RtCore.rt_oc2_set_freq(st.FreqOffset) == 0) ok++; else fail++;
            if (RtCore.rt_oc2_set_volt(st.VoltMv) == 0) ok++; else fail++;
            if (RtCore.rt_oc2_set_power(st.PowerPct) == 0) ok++; else fail++;
            if (st.HasMinFreq && RtCore.rt_oc2_set_min(st.MinFreq) == 0) ok++; else if (st.HasMinFreq) fail++;
            if (st.HasFan && RtCore.rt_set_fan(st.FanPct) == 0) ok++; else if (st.HasFan) fail++;
            return fail == 0 ? $"已回退到 {st.SavedAt} 的状态" : $"回退部分失败：成功 {ok}，失败 {fail}";
        }

        public static bool ApplyPending => File.Exists(PendingPath);
        public static void MarkPending(bool on)
        {
            EnsureDir();
            try
            {
                if (on) File.WriteAllText(PendingPath, DateTime.Now.ToString("o"));
                else if (File.Exists(PendingPath)) File.Delete(PendingPath);
            }
            catch { }
        }

        public static EngineResult ApplySafe(Profile p)
        {
            SaveRollback(CaptureHw());
            MarkPending(true);
            var r = Apply(p);
            MarkPending(false);
            if (!r.Ok)
            {
                RestoreRollback();
                return new EngineResult { Ok = false, Detail = r.Detail + "（已回退到应用前状态）" };
            }
            return r;
        }


        // ---------- 写前记录 ----------
        public static void BeginApply(Profile p)
        {
            EnsureDir();
            Save(StatePath, new Dictionary<string, object>
            {
                ["profile"] = p.Name,
                ["freqOffset"] = p.FreqOffset,
                ["powerPct"] = p.PowerPct,
                ["hasOc"] = p.HasOc,
                ["toggles"] = p.Toggles,
                ["seq"] = 0,
                ["startedAt"] = DateTimeOffset.Now.ToUnixTimeSeconds(),
                ["state"] = "applying",
            });
        }

        public static void FinishApply()
        {
            var st = LoadState();
            st["state"] = "ok";
            st["finishedAt"] = DateTimeOffset.Now.ToUnixTimeSeconds();
            Save(StatePath, st);
            var h = Load(HistoryPath, new History());
            h.LastApply = DateTimeOffset.Now.ToUnixTimeSeconds();
            Save(HistoryPath, h);
        }

        public static Dictionary<string, object> LoadState()
            => Load(StatePath, new Dictionary<string, object>()) ?? new();

        // ---------- 应用（含回滚） ----------
        public static EngineResult Apply(Profile p)
        {
            var before = new Dictionary<string, int>();
            foreach (var k in p.Toggles.Keys) before[k] = RtCore.rt_toggle_get(k);
            var sbBefore = new StringBuilder(128);
            RtCore.rt_oc2_info(sbBefore, sbBefore.Capacity);
            var ocBefore = sbBefore.ToString().Split('|');
            var sbVolt = new StringBuilder(128);
            RtCore.rt_oc2_volt(sbVolt, sbVolt.Capacity);
            var voltBefore = sbVolt.ToString().Split('|');
            var sbMin = new StringBuilder(64);
            RtCore.rt_oc2_min(sbMin, sbMin.Capacity);
            var minBefore = sbMin.ToString().Split('|');

            BeginApply(p);

            foreach (var kv in p.Toggles)
            {
                int rc = RtCore.rt_toggle_set(kv.Key, kv.Value);
                int back = RtCore.rt_toggle_get(kv.Key);
                if (rc != 0 || back != kv.Value)
                {
                    foreach (var b in before) RtCore.rt_toggle_set(b.Key, b.Value);
                    MarkFailed(kv.Key, rc, back);
                    if (!string.IsNullOrEmpty(p.FanCurve)) RtCore.rt_set_fan_curve(p.FanCurve);
            else if (p.FanPct > 0) RtCore.rt_set_fan(p.FanPct);
            return new EngineResult { Ok = false, Action = "failed", Detail = $"{kv.Key} rc={rc} back={back}" };
                }
            }

            if (p.HasOc)
            {
                int rm = 0;
                if (p.MinFreq > 0) rm = RtCore.rt_oc2_set_min(p.MinFreq);
                int rf = RtCore.rt_oc2_set_freq(p.FreqOffset);
                int rp = RtCore.rt_oc2_set_power(p.PowerPct);
                int rv = RtCore.rt_oc2_set_volt(p.VoltMv);
                if (rm != 0 || rf != 0 || rp != 0 || rv != 0)
                {
                    if (minBefore.Length > 2) RtCore.rt_oc2_set_min(int.Parse(minBefore[2]));
                    RtCore.rt_oc2_set_freq(int.Parse(ocBefore[2]));
                    RtCore.rt_oc2_set_power(int.Parse(ocBefore[5]));
                    RtCore.rt_oc2_set_volt(int.Parse(voltBefore[2]));
                    MarkFailed("oc", rf != 0 ? rf : rp, rp);
                    return new EngineResult { Ok = false, Action = "failed", Detail = $"freq rc={rf} power rc={rp} volt rc={rv} min rc={rm}" };
                }
            }

            FinishApply();
            return new EngineResult { Ok = true, Action = "applied" };
        }

        private static void MarkFailed(string item, int rc, int detail)
        {
            var st = LoadState();
            st["state"] = "failed";
            st["failedItem"] = item;
            st["failedRc"] = rc;
            st["failedReadback"] = detail;
            st["failedAt"] = DateTimeOffset.Now.ToUnixTimeSeconds();
            Save(StatePath, st);
        }

        // ---------- 判定与熔断 ----------
        public static EngineResult Judge()
        {
            var st = LoadState();
            if (st.Count == 0) return new EngineResult { Action = "none", Detail = "no_state" };
            var h = Load(HistoryPath, new History());
            if (!st.TryGetValue("state", out var stateObj) || stateObj?.ToString() != "applying")
                return new EngineResult { Action = "none", Detail = "clean" };

            h.Strikes++;
            var name = st.TryGetValue("profile", out var n) ? n.ToString() : "?";

            if (h.Strikes >= BreakerMax)
            {
                if (!h.Quarantined.Contains(name)) h.Quarantined.Add(name);
                Save(HistoryPath, h);
                return new EngineResult { Action = "quarantine", Detail = name };
            }
            Save(HistoryPath, h);
            return new EngineResult
            {
                Action = h.Strikes == 1 ? "replay" : "downgrade_then_replay",
                Detail = $"{name} strikes={h.Strikes}"
            };
        }

        // ---------- 启动闸门 ----------
        public static EngineResult BootGate()
        {
            var h = Load(HistoryPath, new History());
            long newest = 0;
            foreach (var id in new[] { 41, 4101 })
            {
                var t = LatestEventTime(id);
                if (t > newest) newest = t;
            }
            bool unclean = newest > h.LastApply && newest > 0;
            return new EngineResult
            {
                Action = unclean ? "blocked" : "clear",
                Detail = $"newestEvent={newest} lastApply={h.LastApply}",
                Ok = !unclean
            };
        }

        private static long LatestEventTime(int eventId)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "wevtutil",
                    Arguments = $"qe System /q:*[System[(EventID={eventId})]] /c:1 /rd:true /f:text",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                var text = p.StandardOutput.ReadToEnd();
                p.WaitForExit(15000);
                var m = System.Text.RegularExpressions.Regex.Match(text, @"Date:\s*(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})");
                if (!m.Success) return 0;
                var dt = DateTime.ParseExact(m.Groups[1].Value, "yyyy-MM-ddTHH:mm:ss", null);
                return new DateTimeOffset(dt).ToUnixTimeSeconds();
            }
            catch { return 0; }
        }


        public static string OcRoundTripTest()
        {
            var sb = new StringBuilder();
            var info = new StringBuilder(128); RtCore.rt_oc2_info(info, info.Capacity);
            var v = new StringBuilder(64); RtCore.rt_oc2_volt(v, v.Capacity);
            var f = info.ToString().Split('|');
            var vv = v.ToString().Split('|');
            if (f.Length < 6 || vv.Length < 3) return "oc info unavailable";

            int fOff = int.Parse(f[2]), pPct = int.Parse(f[5]), vMv = int.Parse(vv[2]);
            sb.AppendLine($"before: freq={fOff} power={pPct} volt={vMv}");

            var test = new Profile { Name = "octest", HasOc = true,
                                     FreqOffset = fOff + 5, PowerPct = pPct, VoltMv = vMv + 5 };
            var r = Apply(test);
            sb.AppendLine($"apply rc ok={r.Ok} detail={r.Detail}");
            var i2 = new StringBuilder(128); RtCore.rt_oc2_info(i2, i2.Capacity);
            var v2 = new StringBuilder(64); RtCore.rt_oc2_volt(v2, v2.Capacity);
            sb.AppendLine($"after apply: {i2} volt={v2}");

            var restore = new Profile { Name = "octest", HasOc = true,
                                        FreqOffset = fOff, PowerPct = pPct, VoltMv = vMv };
            var r2 = Apply(restore);
            sb.AppendLine($"restore ok={r2.Ok} detail={r2.Detail}");
            var i3 = new StringBuilder(128); RtCore.rt_oc2_info(i3, i3.Capacity);
            var v3 = new StringBuilder(64); RtCore.rt_oc2_volt(v3, v3.Capacity);
            sb.AppendLine($"final: {i3} volt={v3}");
            return sb.ToString();
        }


        /// <summary>按前台进程名查找应自动应用的配置（忽略空匹配与已隔离项）。</summary>
        public static Profile FindProfileForProcess(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return null;
            var all = LoadProfiles();
            var hist = Load(HistoryPath, new History());
            foreach (var kv in all)
            {
                var p = kv.Value;
                if (string.IsNullOrWhiteSpace(p.Process)) continue;
                if (!string.Equals(p.Process, processName, StringComparison.OrdinalIgnoreCase)) continue;
                if (hist.Quarantined.Contains(kv.Key)) continue;
                return p;
            }
            return null;
        }

        // ---------- 自检（模拟崩溃注入） ----------
        public static string SelfTest()
        {
            var log = new StringBuilder();
            try { File.Delete(StatePath); } catch { }
            try { File.Delete(HistoryPath); } catch { }
            Save(HistoryPath, new History());

            var p = new Profile { Name = "selftest", Toggles = new Dictionary<string, int> { ["chill"] = 0 } };

            log.AppendLine("[1] clean apply");
            var r1 = Apply(p);
            log.AppendLine($"    apply ok={r1.Ok} action={r1.Action}");

            for (int i = 1; i <= 3; i++)
            {
                BeginApply(p);
                log.AppendLine($"[{i + 1}] simulated crash while applying");
                var j = Judge();
                log.AppendLine($"    judge -> {j.Action} ({j.Detail})");
            }

            log.AppendLine("[5] gate");
            var g = BootGate();
            log.AppendLine($"    gate -> {g.Action} ({g.Detail})");

            var h = Load(HistoryPath, new History());
            log.AppendLine($"[6] strikes={h.Strikes} quarantined=[{string.Join(",", h.Quarantined)}]");
            return log.ToString();
        }
    }
}
