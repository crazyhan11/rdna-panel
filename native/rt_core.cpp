#include <windows.h>
#include <cstdio>
#include <cstring>
#include <cwchar>
#include <string>
#include <vector>

#include "ADLXHelper.h"
#include "ADLX.h"
#include "IDisplays.h"
#include "IDisplays3.h"
#include "IDisplaySettings.h"
#include "IDisplayGamut.h"
#include "I3DSettings.h"
#include "IGPUTuning.h"
#include "IGPUTuning1.h"
#include "IPerformanceMonitoring.h"
#include "IPerformanceMonitoring1.h"
#include "IPerformanceMonitoring2.h"
#include "I3DSettings1.h"
#include "I3DSettings2.h"
#include "I3DSettings3.h"
#include "IDisplays.h"
#include "IDisplays3.h"
#include "IDisplaySettings.h"
#include "IDisplayGamut.h"
#include "IGPUManualGFXTuning.h"
#include "IGPUManualPowerTuning.h"
#include "IGPUManualFanTuning.h"

using namespace adlx;

#define RT_API extern "C" __declspec(dllexport)

namespace {
struct State {
    IADLXSystem* sys = nullptr;
    IADLXGPU* gpu = nullptr;
    IADLXGPUList* gpus = nullptr;
    int gpuIndex = -1;
    IADLXGPUTuningServices* tuning = nullptr;
    IADLXGPUTuningServices1* tuning1 = nullptr;
    IADLXPerformanceMonitoringServices* pm = nullptr;
    IADLX3DSettingsServices1* d3d1 = nullptr;
    IADLX3DSettingsServices3* d3d3 = nullptr;
    IADLX3DSettingsServices* d3d = nullptr;
    IADLXDisplayServices* dsp = nullptr;
    IADLXDisplay* disp = nullptr;
    bool ready = false;
    char err[256] = {0};
};
State g;
}

static bool ensureInit() {
    if (g.ready) return true;
    static ADLXHelper helper;
    if (helper.Initialize() != ADLX_OK) { strcpy_s(g.err, "ADLX init failed"); return false; }
    g.sys = helper.GetSystemServices();
    if (!g.sys) { strcpy_s(g.err, "no system services"); return false; }
    IADLXGPUList* gpus = nullptr;
    if (g.sys->GetGPUs(&gpus) != ADLX_OK || !gpus || gpus->Size() == 0) { strcpy_s(g.err, "no GPU"); return false; }
    // 必须选独立显卡（RX），列表第 0 项可能是核显
    adlx_uint cnt = gpus->Size();
    for (adlx_uint i = 0; i < cnt; i++) {
        IADLXGPU* cand = nullptr;
        if (gpus->At(i, &cand) != ADLX_OK || !cand) continue;
        const char* nm = nullptr;
        cand->Name(&nm);
        if (nm && strstr(nm, "RX ")) { g.gpu = cand; g.gpuIndex = (int)i; break; }
        cand->Release();
    }
    if (!g.gpu) { gpus->At(0, &g.gpu); g.gpuIndex = 0; }
    g.gpus = gpus;   // 保留列表，供多卡切换
    g.sys->GetGPUTuningServices(&g.tuning);
    g.sys->GetPerformanceMonitoringServices(&g.pm);
    g.sys->Get3DSettingsServices(&g.d3d);
    if (g.sys->GetDisplaysServices(&g.dsp) == ADLX_OK && g.dsp) {
        IADLXDisplayList* dl = nullptr;
        if (g.dsp->GetDisplays(&dl) == ADLX_OK && dl && dl->Size() > 0) {
            dl->At(0, &g.disp);
            dl->Release();
        }
    }
    if (g.d3d) {
        IADLXInterface* i1 = nullptr;
        if (g.d3d->QueryInterface(IADLX3DSettingsServices1::IID(), (void**)&i1) == ADLX_OK && i1) g.d3d1 = static_cast<IADLX3DSettingsServices1*>(i1);
        IADLXInterface* i3 = nullptr;
        if (g.d3d->QueryInterface(IADLX3DSettingsServices3::IID(), (void**)&i3) == ADLX_OK && i3) g.d3d3 = static_cast<IADLX3DSettingsServices3*>(i3);
    }
    if (g.tuning) {
        IADLXInterface* t1 = nullptr;
        if (g.tuning->QueryInterface(IADLXGPUTuningServices1::IID(), (void**)&t1) == ADLX_OK && t1) {
            g.tuning1 = static_cast<IADLXGPUTuningServices1*>(t1);
        }
    }
    g.ready = true;
    return true;
}

RT_API int rt_init() { return ensureInit() ? 0 : -1; }

RT_API const char* rt_last_error() { return g.err; }

RT_API int rt_gpu_info(char* buf, int len) {
    if (!ensureInit() || !buf || len <= 0) return -1;
    const char* name = nullptr;
    g.gpu->Name(&name);
    const char* did = nullptr;
    g.gpu->DeviceId(&did);
    snprintf(buf, (size_t)len, "%s|%s", name ? name : "unknown", did ? did : "?");
    return 0;
}

RT_API int rt_display_info(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    DEVMODEW dm; memset(&dm, 0, sizeof(dm)); dm.dmSize = sizeof(dm);
    if (!EnumDisplaySettingsW(nullptr, ENUM_CURRENT_SETTINGS, &dm)) return -2;
    snprintf(buf, (size_t)len, "display|%d|%d|%d", (int)dm.dmPelsWidth, (int)dm.dmPelsHeight, (int)dm.dmDisplayFrequency);
    return 0;
}

RT_API int rt_mode(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    DEVMODEW dm; memset(&dm, 0, sizeof(dm)); dm.dmSize = sizeof(dm);
    if (!EnumDisplaySettingsW(nullptr, ENUM_CURRENT_SETTINGS, &dm)) return -2;
    snprintf(buf, (size_t)len, "%d|%d|%d|%d", (int)dm.dmPelsWidth, (int)dm.dmPelsHeight,
             (int)dm.dmDisplayFrequency, (int)dm.dmBitsPerPel);
    return 0;
}

RT_API int rt_modes(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    buf[0] = 0;
    int pos = 0;
    DEVMODEW dm; memset(&dm, 0, sizeof(dm)); dm.dmSize = sizeof(dm);
    for (int i = 0; EnumDisplaySettingsW(nullptr, (DWORD)i, &dm); i++) {
        if ((int)dm.dmBitsPerPel < 32) continue;
        int n = snprintf(buf + pos, (size_t)(len - pos), "%s%d,%d,%d", pos ? ";" : "",
                         (int)dm.dmPelsWidth, (int)dm.dmPelsHeight, (int)dm.dmDisplayFrequency);
        if (n <= 0 || pos + n >= len - 1) break;
        pos += n;
    }
    return 0;
}






RT_API int rt_set_mode(int w, int h, int hz) {
    // per-monitor DPI 感知进程里，ChangeDisplaySettingsEx 会因 DEVMODE 的 DPI 上下文不一致返回 DISP_CHANGE_FAILED；
    // 在 UNAWARE 上下文里枚举并提交可避免该问题。
    typedef HANDLE (WINAPI* SetCtxFn)(HANDLE);
    static SetCtxFn pSetCtx = (SetCtxFn)GetProcAddress(GetModuleHandleW(L"user32.dll"), "SetThreadDpiAwarenessContext");
    HANDLE prevCtx = nullptr;
    if (pSetCtx) prevCtx = pSetCtx((HANDLE)-1);   // DPI_AWARENESS_CONTEXT_UNAWARE
    struct Restore { SetCtxFn f; HANDLE c; ~Restore() { if (f && c) f(c); } } restore{ pSetCtx, prevCtx };
    DEVMODEW target; memset(&target, 0, sizeof(target)); target.dmSize = sizeof(target);
    bool found = false;
    for (int i = 0; EnumDisplaySettingsW(nullptr, (DWORD)i, &target); i++) {
        if ((int)target.dmPelsWidth != w || (int)target.dmPelsHeight != h) continue;
        if (hz > 0 && (int)target.dmDisplayFrequency != hz) continue;
        found = true; break;
    }
    if (!found) return -100;
    target.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;
    LONG rc = ChangeDisplaySettingsExW(nullptr, &target, nullptr, CDS_UPDATEREGISTRY, nullptr);
    if (rc == DISP_CHANGE_SUCCESSFUL) return 0;
    DEVMODEW dm; memset(&dm, 0, sizeof(dm)); dm.dmSize = sizeof(dm);
    if (EnumDisplaySettingsW(nullptr, ENUM_CURRENT_SETTINGS, &dm)) {
        dm.dmPelsWidth = (DWORD)w; dm.dmPelsHeight = (DWORD)h; dm.dmDisplayFrequency = (DWORD)hz;
        dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;
        rc = ChangeDisplaySettingsExW(nullptr, &dm, nullptr, CDS_UPDATEREGISTRY, nullptr);
    }
    return (int)rc;
}


// ---------------- 超频读写 ----------------
// 调优接口随世代不同：
//   pre-Navi(Vega/Polaris) 只给 IADLXManualGraphicsTuning1（V/F 状态表，绝对 MHz/mV）
//   Navi 及以后(RDNA1~4) 给 IADLXManualGraphicsTuning2（min/max 频率 + 电压）
//   Navi4(RDNA4) 起 max 频率与电压是相对默认值的偏移；Navi2(RDNA2) 仍是绝对值。
// 这里统一走 v2，并把“绝对值 / 偏移”语义探测出来交给上层 UI。
static IADLXInterface* getGfxItf() {
    if (!ensureInit() || !g.tuning) return nullptr;
    IADLXInterface* itf = nullptr;
    ADLX_RESULT rc = g.tuning1 ? g.tuning1->GetManualGFXTuning(g.gpu, &itf)
                               : g.tuning->GetManualGFXTuning(g.gpu, &itf);
    if (rc != ADLX_OK || !itf) return nullptr;
    return itf;
}

static IADLXManualGraphicsTuning2* getGfx2() {
    IADLXInterface* itf = getGfxItf();
    if (!itf) return nullptr;
    IADLXManualGraphicsTuning2* gfx = nullptr;
    itf->QueryInterface(IADLXManualGraphicsTuning2::IID(), (void**)&gfx);
    itf->Release();
    return gfx;
}

struct OcCaps {
    bool hasV2 = false;
    bool hasV1 = false;
    bool freqAbsolute = false;    // true: MHz 绝对值；false: 相对默认值的偏移
    bool voltAbsolute = false;    // true: mV 绝对值；false: mV 偏移
    bool minFreqSupported = false;
    int  defMaxFreq = 0;          // v2_1 默认值，0 表示驱动未提供
    int  defVolt = 0;
    int  minFreqMin = 0, minFreqMax = 0, minFreqCur = 0;
    int  maxFreqMin = 0, maxFreqMax = 0, maxFreqCur = 0;
    int  voltMin = 0, voltMax = 0, voltCur = 0;
};

static bool queryOcCaps(OcCaps* out) {
    if (!out) return false;
    *out = OcCaps{};
    IADLXInterface* itf = getGfxItf();
    if (!itf) return false;

    IADLXInterface* v1itf = nullptr;
    if (itf->QueryInterface(IADLXManualGraphicsTuning1::IID(), (void**)&v1itf) == ADLX_OK && v1itf) {
        out->hasV1 = true;
        v1itf->Release();
    }

    IADLXManualGraphicsTuning2* gfx = nullptr;
    if (itf->QueryInterface(IADLXManualGraphicsTuning2::IID(), (void**)&gfx) != ADLX_OK || !gfx) {
        itf->Release();
        return false;
    }
    itf->Release();
    out->hasV2 = true;

    ADLX_IntRange mr{}, fr{}, vr{};
    if (gfx->GetGPUMinFrequencyRange(&mr) == ADLX_OK) {
        out->minFreqMin = (int)mr.minValue;
        out->minFreqMax = (int)mr.maxValue;
        out->minFreqSupported = mr.maxValue > 0 && mr.maxValue > mr.minValue;
        adlx_int cur = 0;
        if (gfx->GetGPUMinFrequency(&cur) == ADLX_OK) out->minFreqCur = (int)cur;
    }
    if (gfx->GetGPUMaxFrequencyRange(&fr) == ADLX_OK) {
        out->maxFreqMin = (int)fr.minValue;
        out->maxFreqMax = (int)fr.maxValue;
        adlx_int cur = 0;
        if (gfx->GetGPUMaxFrequency(&cur) == ADLX_OK) out->maxFreqCur = (int)cur;
    }
    if (gfx->GetGPUVoltageRange(&vr) == ADLX_OK) {
        out->voltMin = (int)vr.minValue;
        out->voltMax = (int)vr.maxValue;
        adlx_int cur = 0;
        if (gfx->GetGPUVoltage(&cur) == ADLX_OK) out->voltCur = (int)cur;
    }

    IADLXManualGraphicsTuning2_1* g21 = nullptr;
    if (gfx->QueryInterface(IADLXManualGraphicsTuning2_1::IID(), (void**)&g21) == ADLX_OK && g21) {
        adlx_int v = 0;
        if (g21->GetGPUMaxFrequencyDefault(&v) == ADLX_OK) out->defMaxFreq = (int)v;
        if (g21->GetGPUVoltageDefault(&v) == ADLX_OK) out->defVolt = (int)v;
        g21->Release();
    }
    gfx->Release();

    // 语义判定：
    //  - RDNA2 的默认 max 频率约 2400 MHz、默认电压约 1150 mV，落在绝对量程内；
    //  - RDNA4 的默认值是偏移，通常为 0（或接近 0），且量程跨越 0 / 上限为 0。
    // 优先用驱动默认值判断，默认值不可用时退回量程判断。
    if (out->maxFreqMax > 0) {
        if (out->defMaxFreq > 300) out->freqAbsolute = true;
        else if (out->defMaxFreq == 0 && out->maxFreqMin > 0 && out->maxFreqMin >= 300) out->freqAbsolute = true;
    }
    if (out->voltMax > 0) {
        if (out->defVolt > 300) out->voltAbsolute = true;
        else if (out->defVolt == 0 && out->voltMin > 0 && out->voltMin >= 300) out->voltAbsolute = true;
    }
    return true;
}

static IADLXManualPowerTuning* getPower() {
    if (!ensureInit() || !g.tuning) return nullptr;
    IADLXInterface* itf = nullptr;
    if (g.tuning->GetManualPowerTuning(g.gpu, &itf) != ADLX_OK || !itf) return nullptr;
    IADLXManualPowerTuning* pw = nullptr;
    itf->QueryInterface(IADLXManualPowerTuning::IID(), (void**)&pw);
    itf->Release();
    return pw;
}

RT_API int rt_oc2_info(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    auto* gfx = getGfx2();
    if (!gfx) return -2;
    ADLX_IntRange fr{}, vr{};
    adlx_int fcur = 0;
    gfx->GetGPUMaxFrequencyRange(&fr);
    gfx->GetGPUMaxFrequency(&fcur);
    gfx->Release();
    auto* pw = getPower();
    ADLX_IntRange pr{};
    adlx_int pcur = 0;
    if (pw) {
        pw->GetPowerLimitRange(&pr);
        pw->GetPowerLimit(&pcur);
        pw->Release();
    }
    snprintf(buf, (size_t)len, "%d|%d|%d|%d|%d|%d",
             (int)fr.minValue, (int)fr.maxValue, (int)fcur,
             (int)pr.minValue, (int)pr.maxValue, (int)pcur);
    return 0;
}

RT_API int rt_oc2_volt(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    auto* gfx = getGfx2();
    if (!gfx) return -2;
    ADLX_IntRange vr{};
    adlx_int vcur = 0;
    gfx->GetGPUVoltageRange(&vr);
    gfx->GetGPUVoltage(&vcur);
    gfx->Release();
    snprintf(buf, (size_t)len, "%d|%d|%d", (int)vr.minValue, (int)vr.maxValue, (int)vcur);
    return 0;
}

RT_API int rt_oc2_set_freq(int mhz) {
    auto* gfx = getGfx2();
    if (!gfx) return -2;
    int rc = gfx->SetGPUMaxFrequency(mhz);
    gfx->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}

RT_API int rt_oc2_set_volt(int mv) {
    auto* gfx = getGfx2();
    if (!gfx) return -2;
    int rc = gfx->SetGPUVoltage(mv);
    gfx->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}

RT_API int rt_oc2_set_power(int pct) {
    auto* pw = getPower();
    if (!pw) return -2;
    int rc = pw->SetPowerLimit(pct);
    pw->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}

// 能力/语义：v2|v1|freq_abs|volt_abs|minfreq_sup|default_maxfreq|default_volt
RT_API int rt_oc2_caps(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    OcCaps c{};
    if (!queryOcCaps(&c)) {
        snprintf(buf, (size_t)len, "0|0|0|0|0|0|0");
        return -2;
    }
    snprintf(buf, (size_t)len, "%d|%d|%d|%d|%d|%d|%d",
             c.hasV2 ? 1 : 0, c.hasV1 ? 1 : 0,
             c.freqAbsolute ? 1 : 0, c.voltAbsolute ? 1 : 0,
             c.minFreqSupported ? 1 : 0, c.defMaxFreq, c.defVolt);
    return 0;
}

// 最小频率：min|max|cur
RT_API int rt_oc2_min(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    auto* gfx = getGfx2();
    if (!gfx) return -2;
    ADLX_IntRange r{};
    adlx_int cur = 0;
    int rc = gfx->GetGPUMinFrequencyRange(&r);
    gfx->GetGPUMinFrequency(&cur);
    gfx->Release();
    if (rc != ADLX_OK) return -3;
    snprintf(buf, (size_t)len, "%d|%d|%d", (int)r.minValue, (int)r.maxValue, (int)cur);
    return 0;
}

RT_API int rt_oc2_set_min(int mhz) {
    auto* gfx = getGfx2();
    if (!gfx) return -2;
    int rc = gfx->SetGPUMinFrequency(mhz);
    gfx->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}

RT_API int rt_oc_info(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    if (!ensureInit()) return -2;
    // UI 约定：13 个字段，索引 11 为电压偏移（首页"电压偏移"卡片读取 vf[11]）
    int fmin = 0, fmax = 0, fcur = 0, pmin = 0, pmax = 0, pcur = 0, vmin = 0, vmax = 0, vcur = 0;
    auto* gfx = getGfx2();
    if (gfx) {
        ADLX_IntRange fr{}, vr{};
        gfx->GetGPUMaxFrequencyRange(&fr);
        gfx->GetGPUVoltageRange(&vr);
        gfx->GetGPUMaxFrequency(&fcur);
        gfx->GetGPUVoltage(&vcur);
        fmin = (int)fr.minValue; fmax = (int)fr.maxValue;
        vmin = (int)vr.minValue; vmax = (int)vr.maxValue;
        gfx->Release();
    }
    auto* pw = getPower();
    if (pw) {
        ADLX_IntRange pr{};
        pw->GetPowerLimitRange(&pr);
        pw->GetPowerLimit(&pcur);
        pmin = (int)pr.minValue; pmax = (int)pr.maxValue;
        pw->Release();
    }
    snprintf(buf, (size_t)len, "%d|%d|%d|%d|%d|%d|%d|%d|%d|%d|%d|%d|%d",
             fmin, fmax, fcur, pmin, pmax, pcur, vmin, vmax, vcur, 0, 0, vcur, 0);
    return 0;
}


// ---------------- 图形开关 ----------------
struct ToggleDef { const char* key; const char* iid; IADLXInterface* itf; };
static const char* g_toggleKeys[] = { "antilag", "chill", "boost", "frtc", "ris2", "ensync", "rsr", "fsr", "fg", "afmf", "freesync" };
static const int g_toggleCount = (int)(sizeof(g_toggleKeys) / sizeof(g_toggleKeys[0]));

static IADLXInterface* feature3D(const char* key) {
    if (!ensureInit() || !g.d3d) return nullptr;
    IADLXInterface* itf = nullptr;
    ADLX_RESULT rc = ADLX_FAIL;
    if (!strcmp(key, "antilag")) { IADLX3DAntiLag* f = nullptr; if (g.d3d->GetAntiLag(g.gpu, &f) == ADLX_OK && f) { itf = f; } }
    else if (!strcmp(key, "chill")) { IADLX3DChill* f = nullptr; if (g.d3d->GetChill(g.gpu, &f) == ADLX_OK && f) itf = f; }
    else if (!strcmp(key, "boost")) { IADLX3DBoost* f = nullptr; if (g.d3d->GetBoost(g.gpu, &f) == ADLX_OK && f) itf = f; }
    else if (!strcmp(key, "frtc")) { IADLX3DFrameRateTargetControl* f = nullptr; if (g.d3d->GetFrameRateTargetControl(g.gpu, &f) == ADLX_OK && f) itf = f; }
    else if (!strcmp(key, "ris2")) { IADLX3DImageSharpening* f = nullptr; if (g.d3d->GetImageSharpening(g.gpu, &f) == ADLX_OK && f) itf = f; }
    else if (!strcmp(key, "ensync")) { IADLX3DEnhancedSync* f = nullptr; if (g.d3d->GetEnhancedSync(g.gpu, &f) == ADLX_OK && f) itf = f; }
    else if (!strcmp(key, "rsr") && g.d3d3) { IADLX3DRadeonSuperResolution* f = nullptr; if (g.d3d3->GetRadeonSuperResolution(&f) == ADLX_OK && f) itf = f; }
    else if (!strcmp(key, "fsr") && g.d3d3) { IADLX3DFidelityFXSuperResolution* f = nullptr; if (g.d3d3->GetFidelityFXSuperResolution(g.gpu, &f) == ADLX_OK && f) itf = f; }
    else if (!strcmp(key, "fg") && g.d3d3) { IADLX3DFidelityFXFrameGenUpgrade* f = nullptr; if (g.d3d3->GetFidelityFXFrameGenUpgrade(g.gpu, &f) == ADLX_OK && f) itf = f; }
    else if (!strcmp(key, "afmf") && g.d3d1) { IADLX3DAMDFluidMotionFrames* f = nullptr; if (g.d3d1->GetAMDFluidMotionFrames(&f) == ADLX_OK && f) itf = f; }
    (void)rc;
    return itf;
}

RT_API int rt_toggle_count(void) { return g_toggleCount; }

RT_API const char* rt_toggle_name(int i) {
    if (i < 0 || i >= g_toggleCount) return "";
    return g_toggleKeys[i];
}

RT_API int rt_freesync(char* buf, int len);
RT_API int rt_set_freesync(int on);

RT_API int rt_toggle_get(const char* name) {
    if (!strcmp(name, "freesync")) {
        char b[64]; if (rt_freesync(b, sizeof(b)) != 0) return -1;
        return b[0] == '1' ? 1 : 0;
    }
    auto* itf = feature3D(name);
    if (!itf) return -1;
    adlx_bool en = false;
    auto* base = (IADLX3DAntiLag*)itf;   // 各接口前三个方法布局一致：IsSupported/GetEnabled/SetEnabled
    if (base->IsSupported(&en) != ADLX_OK) { itf->Release(); return -1; }
    if (!en) { itf->Release(); return -2; }
    adlx_bool on = false;
    base->IsEnabled(&on);
    itf->Release();
    return on ? 1 : 0;
}

RT_API int rt_toggle_set(const char* name, int value) {
    if (!strcmp(name, "freesync")) return rt_set_freesync(value);
    auto* itf = feature3D(name);
    if (!itf) return -1;
    auto* base = (IADLX3DAntiLag*)itf;
    adlx_bool on = false;
    if (base->IsSupported(&on) != ADLX_OK || !on) { itf->Release(); return -2; }
    ADLX_RESULT rc = base->SetEnabled(value != 0);
    itf->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}


// ---------------- FreeSync ----------------
static IADLXDisplayFreeSync* getFreeSync() {
    if (!ensureInit() || !g.dsp || !g.disp) return nullptr;
    IADLXDisplayFreeSync* fs = nullptr;
    if (g.dsp->GetFreeSync(g.disp, &fs) != ADLX_OK || !fs) return nullptr;
    return fs;
}

RT_API int rt_freesync(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    auto* fs = getFreeSync();
    if (!fs) return -2;
    adlx_bool sup = false, on = false;
    fs->IsSupported(&sup);
    if (sup) fs->IsEnabled(&on);
    fs->Release();
    snprintf(buf, (size_t)len, "%d|%d", (int)on, (int)sup);
    return 0;
}

RT_API int rt_set_freesync(int on) {
    auto* fs = getFreeSync();
    if (!fs) return -2;
    adlx_bool sup = false;
    fs->IsSupported(&sup);
    if (!sup) { fs->Release(); return -3; }
    ADLX_RESULT rc = fs->SetEnabled(on != 0);
    fs->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}


// ---------------- 风扇 ----------------
static IADLXManualFanTuning* getFan() {
    if (!ensureInit() || !g.tuning) return nullptr;
    IADLXInterface* itf = nullptr;
    if (g.tuning->GetManualFanTuning(g.gpu, &itf) != ADLX_OK || !itf) return nullptr;
    IADLXManualFanTuning* f = nullptr;
    itf->QueryInterface(IADLXManualFanTuning::IID(), (void**)&f);
    itf->Release();
    return f;
}

RT_API int rt_fan(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    auto* f = getFan();
    if (!f) return -2;
    ADLX_IntRange sr{}, tr{};
    f->GetFanTuningRanges(&sr, &tr);
    int cur = 0;
    IADLXManualFanTuningStateList* st = nullptr;
    if (f->GetFanTuningStates(&st) == ADLX_OK && st) {
        IADLXManualFanTuningState* one = nullptr;
        if (st->Size() > 0 && st->At(0, &one) == ADLX_OK && one) {
            adlx_int sp = 0;
            one->GetFanSpeed(&sp);
            cur = (int)sp;
            one->Release();
        }
        st->Release();
    }
    f->Release();
    snprintf(buf, (size_t)len, "%d|%d|%d|%d|%d", (int)sr.minValue, (int)sr.maxValue, cur, (int)tr.minValue, (int)tr.maxValue);
    return 0;
}

RT_API int rt_set_fan(int pct) {
    auto* f = getFan();
    if (!f) return -2;
    IADLXManualFanTuningStateList* st = nullptr;
    if (f->GetFanTuningStates(&st) != ADLX_OK || !st) { f->Release(); return -3; }
    adlx_uint n = st->Size();
    for (adlx_uint i = 0; i < n; i++) {
        IADLXManualFanTuningState* one = nullptr;
        if (st->At(i, &one) != ADLX_OK || !one) continue;
        one->SetFanSpeed(pct);
        one->Release();
    }
    adlx_int bad = -1;
    int rc = -4;
    if (f->IsValidFanTuningStates(st, &bad) == ADLX_OK) rc = f->SetFanTuningStates(st);
    st->Release();
    f->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}

RT_API int rt_fan_curve(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    auto* f = getFan();
    if (!f) return -2;
    IADLXManualFanTuningStateList* st = nullptr;
    if (f->GetFanTuningStates(&st) != ADLX_OK || !st) { f->Release(); return -3; }
    adlx_uint n = st->Size();
    int pos = 0; buf[0] = 0;
    for (adlx_uint i = 0; i < n && pos < len - 24; i++) {
        IADLXManualFanTuningState* one = nullptr;
        if (st->At(i, &one) != ADLX_OK || !one) continue;
        adlx_int t = 0, sp = 0;
        one->GetTemperature(&t);
        one->GetFanSpeed(&sp);
        one->Release();
        pos += snprintf(buf + pos, (size_t)(len - pos), "%s%d:%d", i ? ";" : "", (int)t, (int)sp);
    }
    st->Release();
    f->Release();
    return 0;
}

RT_API int rt_set_fan_curve(const char* spec) {
    if (!spec) return -1;
    auto* f = getFan();
    if (!f) return -2;
    struct Pt { int t, v; };
    Pt pts[16]; int np = 0;
    const char* q = spec;
    while (*q && np < 16) {
        int t = 0, v = 0;
        if (sscanf(q, "%d:%d", &t, &v) != 2) break;
        pts[np].t = t; pts[np].v = v; np++;
        const char* semi = strchr(q, ';');
        if (!semi) break;
        q = semi + 1;
    }
    if (np < 2) { f->Release(); return -4; }

    IADLXManualFanTuningStateList* st = nullptr;
    if (f->GetEmptyFanTuningStates(&st) != ADLX_OK || !st) { f->Release(); return -3; }
    adlx_uint n = st->Size();
    for (adlx_uint i = 0; i < n; i++) {
        IADLXManualFanTuningState* one = nullptr;
        if (st->At(i, &one) != ADLX_OK || !one) continue;
        int idx = (int)i * (np - 1) / (n > 1 ? (int)n - 1 : 1);
        one->SetTemperature(pts[idx].t);
        one->SetFanSpeed(pts[idx].v);
        one->Release();
    }
    adlx_int bad = -1;
    int rc = -5;
    if (f->IsValidFanTuningStates(st, &bad) == ADLX_OK) rc = f->SetFanTuningStates(st);
    st->Release();
    f->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}

RT_API int rt_fan_zero(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    auto* f = getFan();
    if (!f) return -2;
    adlx_bool sup = false, on = false;
    f->IsSupportedZeroRPM(&sup);
    if (sup) f->GetZeroRPMState(&on);
    f->Release();
    snprintf(buf, (size_t)len, "%d|%d", (int)sup, (int)on);
    return 0;
}

RT_API int rt_set_fan_zero(int on) {
    auto* f = getFan();
    if (!f) return -2;
    adlx_bool sup = false;
    f->IsSupportedZeroRPM(&sup);
    if (!sup) { f->Release(); return -3; }
    int rc = f->SetZeroRPMState(on != 0);
    f->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}

RT_API int rt_fan_extra(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    auto* f = getFan();
    if (!f) return -2;
    adlx_bool acSup = false, minSup = false;
    ADLX_IntRange acR{}, minR{};
    adlx_int acCur = 0, minCur = 0;
    f->IsSupportedMinAcousticLimit(&acSup);
    if (acSup) { f->GetMinAcousticLimitRange(&acR); f->GetMinAcousticLimit(&acCur); }
    f->IsSupportedMinFanSpeed(&minSup);
    if (minSup) { f->GetMinFanSpeedRange(&minR); f->GetMinFanSpeed(&minCur); }
    f->Release();
    snprintf(buf, (size_t)len, "%d|%d|%d|%d|%d|%d|%d|%d",
             (int)acSup, (int)acR.minValue, (int)acR.maxValue, (int)acCur,
             (int)minSup, (int)minR.minValue, (int)minR.maxValue, (int)minCur);
    return 0;
}

RT_API int rt_set_fan_acoustic(int v) {
    auto* f = getFan();
    if (!f) return -2;
    int rc = f->SetMinAcousticLimit(v);
    f->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}

RT_API int rt_set_fan_min(int v) {
    auto* f = getFan();
    if (!f) return -2;
    int rc = f->SetMinFanSpeed(v);
    f->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}


// ---------------- 显示：缩放 / VSR / 色深 ----------------
RT_API int rt_scale(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    if (!ensureInit() || !g.dsp || !g.disp) return -2;
    adlx_bool sup = false, gsup = false, gon = false;
    ADLX_SCALE_MODE mode = PRESERVE_ASPECT_RATIO;
    IADLXDisplayScalingMode* sm = nullptr;
    if (g.dsp->GetScalingMode(g.disp, &sm) == ADLX_OK && sm) {
        sm->IsSupported(&sup);
        if (sup) sm->GetMode(&mode);
        sm->Release();
    }
    IADLXDisplayGPUScaling* gs = nullptr;
    if (g.dsp->GetGPUScaling(g.disp, &gs) == ADLX_OK && gs) {
        gs->IsSupported(&gsup);
        if (gsup) gs->IsEnabled(&gon);
        gs->Release();
    }
    snprintf(buf, (size_t)len, "%d|%d|%d|%d", (int)sup, (int)mode, (int)gsup, (int)gon);
    return 0;
}

RT_API int rt_set_scale(int mode) {
    if (!ensureInit() || !g.dsp || !g.disp) return -2;
    IADLXDisplayScalingMode* sm = nullptr;
    if (g.dsp->GetScalingMode(g.disp, &sm) != ADLX_OK || !sm) return -2;
    int rc = sm->SetMode((ADLX_SCALE_MODE)mode);
    sm->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}

RT_API int rt_set_gpuscaling(int on) {
    if (!ensureInit() || !g.dsp || !g.disp) return -2;
    IADLXDisplayGPUScaling* gs = nullptr;
    if (g.dsp->GetGPUScaling(g.disp, &gs) != ADLX_OK || !gs) return -2;
    int rc = gs->SetEnabled(on != 0);
    gs->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}

RT_API int rt_vsr(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    if (!ensureInit() || !g.dsp || !g.disp) return -2;
    IADLXDisplayVSR* v = nullptr;
    if (g.dsp->GetVirtualSuperResolution(g.disp, &v) != ADLX_OK || !v) return -2;
    adlx_bool sup = false, on = false;
    v->IsSupported(&sup);
    if (sup) v->IsEnabled(&on);
    v->Release();
    snprintf(buf, (size_t)len, "%d|%d", (int)sup, (int)on);
    return 0;
}

RT_API int rt_set_vsr(int on) {
    if (!ensureInit() || !g.dsp || !g.disp) return -2;
    IADLXDisplayVSR* v = nullptr;
    if (g.dsp->GetVirtualSuperResolution(g.disp, &v) != ADLX_OK || !v) return -2;
    int rc = v->SetEnabled(on != 0);
    v->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}

RT_API int rt_bpc(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    if (!ensureInit() || !g.dsp || !g.disp) return -2;
    IADLXDisplayColorDepth* cd = nullptr;
    if (g.dsp->GetColorDepth(g.disp, &cd) != ADLX_OK || !cd) return -2;
    ADLX_COLOR_DEPTH v = BPC_UNKNOWN;
    adlx_bool s6 = false, s8 = false, s10 = false, s12 = false, s14 = false, s16 = false;
    cd->GetValue(&v);
    cd->IsSupportedBPC_6(&s6);
    cd->IsSupportedBPC_8(&s8);
    cd->IsSupportedBPC_10(&s10);
    cd->IsSupportedBPC_12(&s12);
    cd->IsSupportedBPC_14(&s14);
    cd->IsSupportedBPC_16(&s16);
    cd->Release();
    // UI 约定：字段 0 为当前 ADLX 枚举值，其后 6 个是该位深是否支持的标志
    snprintf(buf, (size_t)len, "%d|%d|%d|%d|%d|%d|%d",
             (int)v, (int)s6, (int)s8, (int)s10, (int)s12, (int)s14, (int)s16);
    return 0;
}

RT_API int rt_set_bpc(int v) {
    if (!ensureInit() || !g.dsp || !g.disp) return -2;
    IADLXDisplayColorDepth* cd = nullptr;
    if (g.dsp->GetColorDepth(g.disp, &cd) != ADLX_OK || !cd) return -2;
    int rc = cd->SetValue((ADLX_COLOR_DEPTH)v);
    cd->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}



// ---------------- 像素格式 / 色域 / EDID ----------------
RT_API int rt_pixfmt(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    if (!ensureInit() || !g.dsp || !g.disp) return -2;
    IADLXDisplayPixelFormat* pf = nullptr;
    if (g.dsp->GetPixelFormat(g.disp, &pf) != ADLX_OK || !pf) return -2;
    adlx_bool sup = false;
    ADLX_PIXEL_FORMAT fmt = FORMAT_UNKNOWN;
    pf->IsSupported(&sup);
    if (sup) pf->GetValue(&fmt);
    pf->Release();
    snprintf(buf, (size_t)len, "%d|%d", (int)fmt, (int)sup);
    return 0;
}

RT_API int rt_set_pixfmt(int v) {
    if (!ensureInit() || !g.dsp || !g.disp) return -2;
    IADLXDisplayPixelFormat* pf = nullptr;
    if (g.dsp->GetPixelFormat(g.disp, &pf) != ADLX_OK || !pf) return -2;
    int rc = pf->SetValue((ADLX_PIXEL_FORMAT)v);
    pf->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}

RT_API int rt_gamut(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    if (!ensureInit() || !g.dsp || !g.disp) return -2;
    IADLXDisplayGamut* gm = nullptr;
    if (g.dsp->GetGamut(g.disp, &gm) != ADLX_OK || !gm) return -2;
    adlx_bool sup = false;
    gm->IsSupported6500kWhitePoint(&sup);
    int x = 0, y = 0;
    if (sup) {
        ADLX_Point pt{};
        if (gm->GetWhitePoint(&pt) == ADLX_OK) { x = (int)pt.x; y = (int)pt.y; }
    }
    gm->Release();
    snprintf(buf, (size_t)len, "%d|%d|%d", (int)sup, x, y);
    return 0;
}

// EDID 从注册表读取（不依赖 ADLX）
RT_API int rt_edid(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    buf[0] = 0;
    HKEY displays = nullptr;
    if (RegOpenKeyExW(HKEY_LOCAL_MACHINE,
            L"SYSTEM\\CurrentControlSet\\Enum\\DISPLAY", 0, KEY_READ, &displays) != ERROR_SUCCESS)
        return -2;

    wchar_t vendor[256];
    DWORD vlen = 256;
    int written = 0;
    for (DWORD vi = 0; RegEnumKeyExW(displays, vi, vendor, &vlen, nullptr, nullptr, nullptr, nullptr) == ERROR_SUCCESS; vi++, vlen = 256) {
        HKEY vk = nullptr;
        if (RegOpenKeyExW(displays, vendor, 0, KEY_READ, &vk) != ERROR_SUCCESS) continue;
        wchar_t inst[256];
        DWORD ilen = 256;
        for (DWORD ii = 0; RegEnumKeyExW(vk, ii, inst, &ilen, nullptr, nullptr, nullptr, nullptr) == ERROR_SUCCESS; ii++, ilen = 256) {
            HKEY ik = nullptr;
            if (RegOpenKeyExW(vk, inst, 0, KEY_READ, &ik) != ERROR_SUCCESS) continue;
            HKEY pk = nullptr;
            if (RegOpenKeyExW(ik, L"Device Parameters", 0, KEY_READ, &pk) == ERROR_SUCCESS) {
                BYTE data[512];
                DWORD size = sizeof(data), type = 0;
                if (RegQueryValueExW(pk, L"EDID", nullptr, &type, data, &size) == ERROR_SUCCESS && size >= 128) {
                    for (DWORD b = 0; b < size && written < len - 3; b++)
                        written += snprintf(buf + written, (size_t)(len - written), "%02x", data[b]);
                    RegCloseKey(pk);
                    RegCloseKey(ik);
                    RegCloseKey(vk);
                    RegCloseKey(displays);
                    return written > 0 ? 0 : -3;
                }
                RegCloseKey(pk);
            }
            RegCloseKey(ik);
        }
        RegCloseKey(vk);
    }
    RegCloseKey(displays);
    return -3;
}


// ---------------- AFMF 算法 / 搜索模式 ----------------
static IADLX3DAMDFluidMotionFrames1* getAFMF1() {
    if (!ensureInit() || !g.d3d1) return nullptr;
    IADLX3DAMDFluidMotionFrames* base = nullptr;
    if (g.d3d1->GetAMDFluidMotionFrames(&base) != ADLX_OK || !base) return nullptr;
    IADLX3DAMDFluidMotionFrames1* f1 = nullptr;
    base->QueryInterface(IADLX3DAMDFluidMotionFrames1::IID(), (void**)&f1);
    base->Release();
    return f1;
}

RT_API int rt_afmf(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    if (!ensureInit() || !g.d3d1) return -2;
    IADLX3DAMDFluidMotionFrames* base = nullptr;
    if (g.d3d1->GetAMDFluidMotionFrames(&base) != ADLX_OK || !base) return -2;
    adlx_bool sup = false, on = false;
    base->IsSupported(&sup);
    if (sup) base->IsEnabled(&on);
    int algo = 0, sm = 0;
    IADLX3DAMDFluidMotionFrames1* f1 = nullptr;
    if (base->QueryInterface(IADLX3DAMDFluidMotionFrames1::IID(), (void**)&f1) == ADLX_OK && f1) {
        ADLX_AFMF_ALGORITHM a = AFMF_ALGORITHM_AUTO;
        ADLX_AFMF_SEARCH_MODE_TYPE m = AFMF_SEARCH_MODE_AUTO;
        if (f1->GetAlgorithm(&a) == ADLX_OK) algo = (int)a;
        if (f1->GetSearchMode(&m) == ADLX_OK) sm = (int)m;
        f1->Release();
    }
    base->Release();
    snprintf(buf, (size_t)len, "%d|%d|%d", (int)on, algo, sm);
    return 0;
}

RT_API int rt_set_afmf_algo(int v) {
    auto* f1 = getAFMF1();
    if (!f1) return -2;
    int rc = f1->SetAlgorithm((ADLX_AFMF_ALGORITHM)v);
    f1->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}

RT_API int rt_set_afmf_search(int v) {
    auto* f1 = getAFMF1();
    if (!f1) return -2;
    int rc = f1->SetSearchMode((ADLX_AFMF_SEARCH_MODE_TYPE)v);
    f1->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}


// ---------------- 自定义分辨率（只读列表） ----------------
RT_API int rt_customres(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    if (!ensureInit() || !g.dsp || !g.disp) return -2;
    IADLXDisplayCustomResolution* cr = nullptr;
    if (g.dsp->GetCustomResolution(g.disp, &cr) != ADLX_OK || !cr) return -2;
    adlx_bool sup = false;
    cr->IsSupported(&sup);
    if (!sup) { cr->Release(); return -2; }
    IADLXDisplayResolutionList* list = nullptr;
    int count = 0;
    if (cr->GetResolutionList(&list) == ADLX_OK && list) {
        adlx_uint n = list->Size();
        int pos = 0; buf[0] = 0;
        for (adlx_uint i = 0; i < n && pos < len - 32; i++) {
            IADLXDisplayResolution* item = nullptr;
            if (list->At(i, &item) != ADLX_OK || !item) continue;
            ADLX_CustomResolution v{};
            if (item->GetValue(&v) == ADLX_OK) {
                pos += snprintf(buf + pos, (size_t)(len - pos), "%s%dx%d@%d",
                                count ? ";" : "", (int)v.resWidth, (int)v.resHeight, (int)v.refreshRate);
                count++;
            }
            item->Release();
        }
        list->Release();
    }
    cr->Release();
    return count;
}

RT_API int rt_cr_current(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    if (!ensureInit() || !g.dsp || !g.disp) return -2;
    IADLXDisplayCustomResolution* cr = nullptr;
    if (g.dsp->GetCustomResolution(g.disp, &cr) != ADLX_OK || !cr) return -2;
    IADLXDisplayResolution* cur = nullptr;
    if (cr->GetCurrentAppliedResolution(&cur) != ADLX_OK || !cur) { cr->Release(); return -2; }
    ADLX_CustomResolution v{};
    int rc = cur->GetValue(&v);
    cur->Release();
    cr->Release();
    if (rc != ADLX_OK) return -3;
    // UI 约定：17 个逗号分隔字段（CustomResDialog / --crtest2 都按此索引解析）
    snprintf(buf, (size_t)len, "%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",
             (int)v.resWidth, (int)v.resHeight, (int)v.refreshRate,
             (int)v.presentation, (int)v.timingStandard, (int)v.GPixelClock,
             (int)v.detailedTiming.timingFlags,
             (int)v.detailedTiming.hTotal, (int)v.detailedTiming.vTotal,
             (int)v.detailedTiming.hDisplay, (int)v.detailedTiming.vDisplay,
             (int)v.detailedTiming.hFrontPorch, (int)v.detailedTiming.vFrontPorch,
             (int)v.detailedTiming.hSyncWidth, (int)v.detailedTiming.vSyncWidth,
             (int)v.detailedTiming.hPolarity, (int)v.detailedTiming.vPolarity);
    return 0;
}


// ---------------- 锐化（ADLX 图像锐化强度） ----------------
static IADLX3DImageSharpening* getSharpening() {
    if (!ensureInit() || !g.d3d) return nullptr;
    IADLX3DImageSharpening* f = nullptr;
    if (g.d3d->GetImageSharpening(g.gpu, &f) != ADLX_OK || !f) return nullptr;
    return f;
}

RT_API int rt_rsr_sharp2(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    auto* f = getSharpening();
    if (!f) return -2;
    adlx_bool sup = false, en = false;
    ADLX_IntRange r{};
    adlx_int cur = 0;
    f->IsSupported(&sup);
    if (sup) {
        f->IsEnabled(&en);
        f->GetSharpnessRange(&r);
        f->GetSharpness(&cur);
    }
    f->Release();
    snprintf(buf, (size_t)len, "%d|%d|%d", (int)sup, (int)en, (int)(cur / 10));
    return 0;
}

RT_API int rt_set_rsr_sharp(int v) {
    auto* f = getSharpening();
    if (!f) return -2;
    int rc = f->SetSharpness(v);
    f->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}

// ---------------- 驱动可调范围（只读） ----------------
RT_API int rt_od8_limits(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    if (!ensureInit()) return -2;
    OcCaps c{};
    queryOcCaps(&c);
    int pmin = 0, pmax = 0, pcur = 0;
    auto* pw = getPower();
    if (pw) {
        ADLX_IntRange pr{};
        pw->GetPowerLimitRange(&pr);
        pw->GetPowerLimit(&pcur);
        pmin = (int)pr.minValue; pmax = (int)pr.maxValue;
        pw->Release();
    }
    snprintf(buf, (size_t)len,
             "freqmin=%d freqmax=%d freqcur=%d minfreqmin=%d minfreqmax=%d minfreqcur=%d "
             "pwrmin=%d pwrmax=%d pwrcur=%d voltmin=%d voltmax=%d voltcur=%d fabs=%d vabs=%d minsup=%d",
             c.maxFreqMin, c.maxFreqMax, c.maxFreqCur,
             c.minFreqMin, c.minFreqMax, c.minFreqCur,
             pmin, pmax, pcur,
             c.voltMin, c.voltMax, c.voltCur,
             c.freqAbsolute ? 1 : 0, c.voltAbsolute ? 1 : 0, c.minFreqSupported ? 1 : 0);
    return 0;
}


RT_API int rt_cr_del(int index) {
    if (!ensureInit() || !g.dsp || !g.disp) return -2;
    IADLXDisplayCustomResolution* cr = nullptr;
    if (g.dsp->GetCustomResolution(g.disp, &cr) != ADLX_OK || !cr) return -2;
    IADLXDisplayResolutionList* list = nullptr;
    if (cr->GetResolutionList(&list) != ADLX_OK || !list) { cr->Release(); return -3; }
    IADLXDisplayResolution* item = nullptr;
    if (index < 0 || (adlx_uint)index >= list->Size() || list->At((adlx_uint)index, &item) != ADLX_OK || !item) {
        list->Release(); cr->Release(); return -4;
    }
    int rc = cr->DeleteResolution(item);
    item->Release();
    list->Release();
    cr->Release();
    return rc == ADLX_OK ? 0 : -100 + rc;
}

// 限制：ADLX 只提供 IADLXDisplayResolutionList::Add_Back(IADLXDisplayResolution*)，
// 但没有"从零构造 IADLXDisplayResolution"的工厂；列表为空时无法克隆出条目。
// Add 通过 GetCurrentAppliedResolution() 克隆当前时序后修改目标字段实现。
//
// 额外防护（驱动 1.4 的 CreateNewResolution/DeleteResolution 很挑输入）：
//   - 已存在的自定义模式直接拒绝（同一 w/h/hz 重复创建会留下删不掉的幽灵条目）
//   - 系统已原生支持的模式直接拒绝（驱动会返回 INVALID_ARGS）
//   - 创建后回读列表确认条目真的出现
RT_API int rt_cr_add_ex(int w, int h, int hz, int presentation, int timingStandard, int pixClock,
                        int tFlags, int hTotal, int vTotal, int hDisp, int vDisp,
                        int hFront, int vFront, int hSync, int vSync, int hPol, int vPol) {
    if (!ensureInit() || !g.dsp || !g.disp) return -2;
    if (w <= 0 || h <= 0 || hz <= 0) return -3;

    IADLXDisplayCustomResolution* cr = nullptr;
    if (g.dsp->GetCustomResolution(g.disp, &cr) != ADLX_OK || !cr) return -2;

    // 注意：IsSupported 在 ADLX 1.4 上不稳定（已有条目时可能返回 false，但创建仍会成功），
    // 这里不再用它硬拦截，交给 CreateNewResolution 的返回值判断。

    // 列表里已有同一 w/h/refresh 的条目则拒绝，避免重复创建导致无法删除的幽灵模式
    IADLXDisplayResolutionList* list0 = nullptr;
    if (cr->GetResolutionList(&list0) == ADLX_OK && list0) {
        for (adlx_uint i = 0; i < list0->Size(); i++) {
            IADLXDisplayResolution* item = nullptr;
            if (list0->At(i, &item) != ADLX_OK || !item) continue;
            ADLX_CustomResolution cv{};
            bool dup = item->GetValue(&cv) == ADLX_OK &&
                       (int)cv.resWidth == w && (int)cv.resHeight == h && (int)cv.refreshRate == hz;
            item->Release();
            if (dup) { list0->Release(); cr->Release(); return -200; }
        }
        list0->Release();
    }

    // 取当前生效分辨率作为模板对象（ADLX 未提供从零构造的工厂）
    IADLXDisplayResolution* res = nullptr;
    if (cr->GetCurrentAppliedResolution(&res) != ADLX_OK || !res) { cr->Release(); return -5; }

    ADLX_CustomResolution v{};
    if (res->GetValue(&v) != ADLX_OK) { res->Release(); cr->Release(); return -6; }

    v.resWidth = w;
    v.resHeight = h;
    v.refreshRate = hz;
    v.presentation = (ADLX_DISPLAY_SCAN_TYPE)presentation;
    v.timingStandard = (ADLX_TIMING_STANDARD)timingStandard;
    if (pixClock > 0) v.GPixelClock = pixClock;

    v.detailedTiming.timingFlags = tFlags;
    v.detailedTiming.hTotal = hTotal;
    v.detailedTiming.vTotal = vTotal;
    v.detailedTiming.hDisplay = hDisp > 0 ? hDisp : w;
    v.detailedTiming.vDisplay = vDisp > 0 ? vDisp : h;
    v.detailedTiming.hFrontPorch = hFront;
    v.detailedTiming.vFrontPorch = vFront;
    v.detailedTiming.hSyncWidth = hSync;
    v.detailedTiming.vSyncWidth = vSync;
    v.detailedTiming.hPolarity = hPol;
    v.detailedTiming.vPolarity = vPol;

    if (res->SetValue(v) != ADLX_OK) { res->Release(); cr->Release(); return -7; }

    ADLX_RESULT rc = cr->CreateNewResolution(res);
    res->Release();
    if (rc != ADLX_OK) { cr->Release(); return -100 + (int)rc; }

    // 回读确认：列表里必须出现目标模式，否则视为失败（驱动可能静默丢弃）
    bool found = false;
    IADLXDisplayResolutionList* list1 = nullptr;
    if (cr->GetResolutionList(&list1) == ADLX_OK && list1) {
        for (adlx_uint i = 0; i < list1->Size(); i++) {
            IADLXDisplayResolution* item = nullptr;
            if (list1->At(i, &item) != ADLX_OK || !item) continue;
            ADLX_CustomResolution cv{};
            if (item->GetValue(&cv) == ADLX_OK &&
                (int)cv.resWidth == w && (int)cv.resHeight == h && (int)cv.refreshRate == hz) found = true;
            item->Release();
            if (found) break;
        }
        list1->Release();
    }
    cr->Release();
    return found ? 0 : -202;
}

// 清理驱动持久化的自定义分辨率（DAL3_DATA\common\EDID_*\mode_timing_override）。
// 驱动 1.4 在列表进入异常状态后 DeleteResolution 会一直返回 INVALID_ARGS，
// 只能清掉注册表里的持久化数据并重启显卡驱动/系统。
// 返回删除的条目数；-1 参数/权限错误。
RT_API int rt_cr_reset(int keepBackup) {
    const wchar_t* base = L"SYSTEM\\CurrentControlSet\\Control\\Class\\"
                          L"{4d36e968-e325-11ce-bfc1-08002be10318}\\0000\\DAL3_DATA\\common";
    HKEY common = nullptr;
    if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, base, 0, KEY_READ | KEY_WRITE, &common) != ERROR_SUCCESS) return -1;

    wchar_t sub[256];
    DWORD idx = 0, slen = 256;
    int removed = 0;
    while (RegEnumKeyExW(common, idx++, sub, &slen, nullptr, nullptr, nullptr, nullptr) == ERROR_SUCCESS) {
        slen = 256;
        if (wcsncmp(sub, L"EDID_", 5) != 0) continue;
        HKEY k = nullptr;
        if (RegOpenKeyExW(common, sub, 0, KEY_READ | KEY_WRITE, &k) != ERROR_SUCCESS) continue;
        DWORD type = 0, size = 0;
        if (RegQueryValueExW(k, L"mode_timing_override", nullptr, &type, nullptr, &size) == ERROR_SUCCESS &&
            type == REG_BINARY && size > 0) {
            if (keepBackup) {
                std::vector<BYTE> buf(size);
                DWORD rsize = size;
                if (!buf.empty() && RegQueryValueExW(k, L"mode_timing_override", nullptr, &type, buf.data(), &rsize) == ERROR_SUCCESS) {
                    wchar_t path[MAX_PATH] = {0};
                    DWORD n = GetEnvironmentVariableW(L"LOCALAPPDATA", path, MAX_PATH);
                    if (n > 0) {
                        wchar_t dir[MAX_PATH] = {0};
                        swprintf_s(dir, L"%ls\\RDNA Panel", path);
                        CreateDirectoryW(dir, nullptr);
                        wchar_t file[MAX_PATH] = {0};
                        SYSTEMTIME st; GetLocalTime(&st);
                        swprintf_s(file, L"%ls\\cr-backup-%04d%02d%02d-%02d%02d%02d.bin",
                                   dir, st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond);
                        HANDLE hf = CreateFileW(file, GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
                        if (hf != INVALID_HANDLE_VALUE) {
                            DWORD written = 0;
                            WriteFile(hf, buf.data(), rsize, &written, nullptr);
                            CloseHandle(hf);
                        }
                    }
                }
            }
            if (RegDeleteValueW(k, L"mode_timing_override") == ERROR_SUCCESS) removed++;
        }
        RegCloseKey(k);
    }
    RegCloseKey(common);
    return removed;
}

RT_API int rt_smu_available(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    snprintf(buf, (size_t)len, "0|0");
    return 0;
}






RT_API int rt_dbg(char* buf, int len) {
    if (!ensureInit()) { snprintf(buf, (size_t)len, "init fail: %s", g.err); return 0; }
    int t = g.tuning ? 1 : 0;
    IADLXInterface* itf = nullptr;
    int rc = g.tuning1 ? (int)g.tuning1->GetManualGFXTuning(g.gpu, &itf)
                       : (g.tuning ? (int)g.tuning->GetManualGFXTuning(g.gpu, &itf) : -1);
    IADLXManualGraphicsTuning2* gfx = nullptr;
    int qi = -1;
    if (itf) { qi = (int)itf->QueryInterface(IADLXManualGraphicsTuning2::IID(), (void**)&gfx); }
    OcCaps c{};
    queryOcCaps(&c);
    snprintf(buf, (size_t)len,
             "t1=%d tuning=%d rc=%d itf=%d qi=%d gfx=%d | v1=%d fabs=%d vabs=%d minsup=%d defmax=%d defvolt=%d",
             g.tuning1 ? 1 : 0, t, rc, itf ? 1 : 0, qi, gfx ? 1 : 0,
             c.hasV1 ? 1 : 0, c.freqAbsolute ? 1 : 0, c.voltAbsolute ? 1 : 0,
             c.minFreqSupported ? 1 : 0, c.defMaxFreq, c.defVolt);
    if (gfx) gfx->Release();
    if (itf) itf->Release();
    return 0;
}

RT_API int rt_mon(char* buf, int len) {
    if (!buf || len <= 0) return -1;
    if (!ensureInit() || !g.pm) return -2;
    IADLXGPUMetrics* m = nullptr;
    if (g.pm->GetCurrentGPUMetrics(g.gpu, &m) != ADLX_OK || !m) return -3;
    adlx_double temp = 0, hot = 0, usage = 0, power = 0, board = 0, memTemp = 0;
    adlx_int clock = 0, vram = 0, volt = 0, fan = 0;
    m->GPUClockSpeed(&clock);
    m->GPUVRAMClockSpeed(&vram);
    m->GPUTemperature(&temp);
    m->GPUHotspotTemperature(&hot);
    m->GPUVoltage(&volt);
    m->GPUFanSpeed(&fan);
    m->GPUUsage(&usage);
    m->GPUPower(&power);
    m->GPUTotalBoardPower(&board);

    IADLXGPUMetrics1* m1 = nullptr;
    if (m->QueryInterface(IADLXGPUMetrics1::IID(), (void**)&m1) == ADLX_OK && m1) {
        m1->GPUMemoryTemperature(&memTemp);
        m1->Release();
    }
    m->Release();
    // UI 约定 11 字段：0 时钟 1 显存 2 边缘温 3 显存温 4 电压 5 热点 6 风扇rpm 7 风扇% 8 整卡功耗 9 占用 10 保留
    snprintf(buf, (size_t)len, "%d|%d|%d|%d|%d|%d|%d|%d|%d|%d|%d",
             (int)clock, (int)vram, (int)temp, (int)memTemp, (int)volt, (int)hot,
             (int)fan, (int)fan, (int)(board > 0 ? board : power), (int)usage, 0);
    return 0;
}

// ---------------- 多显卡 ----------------
RT_API int rt_gpu_count(void) {
    if (!ensureInit() || !g.gpus) return 0;
    return (int)g.gpus->Size();
}

static void setGpuIndex(int idx) {
    if (!g.gpus) return;
    IADLXGPU* cand = nullptr;
    if (g.gpus->At((adlx_uint)idx, &cand) != ADLX_OK || !cand) return;
    if (g.gpu) g.gpu->Release();
    g.gpu = cand;
    g.gpuIndex = idx;
    // 显示服务与该卡绑定：重新取一次显示列表
    if (g.disp) { g.disp->Release(); g.disp = nullptr; }
    if (g.dsp) {
        IADLXDisplayList* dl = nullptr;
        if (g.dsp->GetDisplays(&dl) == ADLX_OK && dl && dl->Size() > 0) { dl->At(0, &g.disp); }
        if (dl) dl->Release();
    }
}

RT_API int rt_gpu_select(int idx) {
    if (!ensureInit() || !g.gpus) return -2;
    if (idx < 0 || (adlx_uint)idx >= g.gpus->Size()) return -3;
    setGpuIndex(idx);
    return 0;
}

RT_API int rt_gpu_name(int idx, char* buf, int len) {
    if (!buf || len <= 0) return -1;
    if (!ensureInit() || !g.gpus) return -2;
    if (idx < 0 || (adlx_uint)idx >= g.gpus->Size()) return -3;
    IADLXGPU* cand = nullptr;
    if (g.gpus->At((adlx_uint)idx, &cand) != ADLX_OK || !cand) return -3;
    const char* nm = nullptr; const char* did = nullptr;
    cand->Name(&nm);
    cand->DeviceId(&did);
    snprintf(buf, (size_t)len, "%s|%s", nm ? nm : "?", did ? did : "?");
    cand->Release();
    return 0;
}

// 指定显卡的监控（多卡同时展示）
RT_API int rt_mon_at(int idx, char* buf, int len) {
    if (!buf || len <= 0) return -1;
    if (!ensureInit() || !g.pm || !g.gpus) return -2;
    if (idx < 0 || (adlx_uint)idx >= g.gpus->Size()) return -3;
    IADLXGPU* card = nullptr;
    if (g.gpus->At((adlx_uint)idx, &card) != ADLX_OK || !card) return -3;
    IADLXGPUMetrics* m = nullptr;
    int rc = g.pm->GetCurrentGPUMetrics(card, &m);
    if (rc != ADLX_OK || !m) { card->Release(); return -3; }
    adlx_double temp = 0, hot = 0, usage = 0, power = 0, board = 0, memTemp = 0;
    adlx_int clock = 0, vram = 0, volt = 0, fan = 0;
    m->GPUClockSpeed(&clock);
    m->GPUVRAMClockSpeed(&vram);
    m->GPUTemperature(&temp);
    m->GPUHotspotTemperature(&hot);
    m->GPUVoltage(&volt);
    m->GPUFanSpeed(&fan);
    m->GPUUsage(&usage);
    m->GPUPower(&power);
    m->GPUTotalBoardPower(&board);
    IADLXGPUMetrics1* m1 = nullptr;
    if (m->QueryInterface(IADLXGPUMetrics1::IID(), (void**)&m1) == ADLX_OK && m1) { m1->GPUMemoryTemperature(&memTemp); m1->Release(); }
    m->Release();
    card->Release();
    snprintf(buf, (size_t)len, "%d|%d|%d|%d|%d|%d|%d|%d|%d|%d|%d",
             (int)clock, (int)vram, (int)temp, (int)memTemp, (int)volt, (int)hot,
             (int)fan, (int)fan, (int)(board > 0 ? board : power), (int)usage, 0);
    return 0;
}
