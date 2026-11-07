using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace RadeonTune
{
    public static class RtCore
    {
        private const string Dll = "rt_core.dll";

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_init();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr rt_last_error();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_toggle_count();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr rt_toggle_name(int i);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_toggle_get(string name);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_toggle_set(string name, int value);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_display_info(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_gpu_info(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_gpu_count();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_mon_at(int idx, StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int rt_gpu_name(int idx, StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_gpu_select(int idx);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_oc2_info(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_oc_info(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_oc2_volt(StringBuilder buf, int len);

        // v2|v1|freq_abs|volt_abs|minfreq_sup|default_maxfreq|default_volt
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_oc2_caps(StringBuilder buf, int len);

        // min|max|cur
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_oc2_min(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_oc2_set_min(int mhz);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_oc2_set_volt(int mv);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_mon(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_mode(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_bpc(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_set_bpc(int v);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_gamut(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_pixfmt(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_rsr_sharp2(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_set_rsr_sharp(int strength);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_fan(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_set_fan(int pct);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_fan_curve(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int rt_set_fan_curve(string spec);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_fan_zero(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_set_fan_zero(int on);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_fan_extra(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_set_fan_acoustic(int v);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_set_fan_min(int v);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_smu_available(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_od8_limits(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_afmf(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_set_afmf_algo(int v);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_set_afmf_search(int v);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_edid(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_cr_current(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_cr_add_ex(int w, int h, int hz, int presentation, int timingStandard, int pixClock,
            int tFlags, int hTotal, int vTotal, int hDisp, int vDisp,
            int hFront, int vFront, int hSync, int vSync, int hPol, int vPol);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_cr_del(int index);

        // 清理驱动持久化的自定义分辨率（DAL3_DATA\common\EDID_*\mode_timing_override）
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_cr_reset(int keepBackup);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_scale(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_set_scale(int mode);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_set_gpuscaling(int on);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_vsr(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_set_vsr(int on);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_customres(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_freesync(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_set_freesync(int on);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_modes(StringBuilder buf, int len);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_set_mode(int w, int h, int hz);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_set_pixfmt(int v);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_oc2_set_freq(int mhz);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int rt_oc2_set_power(int pct);

        public static string LastError()
        {
            try { return Marshal.PtrToStringAnsi(rt_last_error()) ?? ""; }
            catch { return ""; }
        }

        public static string DisplayInfo()
        {
            var sb = new StringBuilder(256);
            return rt_display_info(sb, sb.Capacity) == 0 ? sb.ToString() : "";
        }

        public static string GpuInfo()
        {
            var sb = new StringBuilder(256);
            return rt_gpu_info(sb, sb.Capacity) == 0 ? sb.ToString() : "";
        }

        public static string ToggleName(int i)
        {
            var p = rt_toggle_name(i);
            return p == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(p);
        }
    }
}
