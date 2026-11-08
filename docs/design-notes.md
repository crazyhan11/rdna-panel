# Design notes and rationale

This file records **why** RDNA Panel looks the way it does, and — importantly — which statements are
observations, which are analysis, and which are inference.

Status legend: **Observed** = reproduced locally; **Analysis** = reasoned from how the software is
built; **Inference** = plausible but NOT measured.

## 1. The vendor panel: what actually hangs on Explorer

**Observed on the target machine (module list of `explorer.exe`):**

- `amdihk64.dll` is loaded into `explorer.exe` and registered as **"Radeon Settings: Host Service"**,
  CLSID `{CCB5F028-A16A-4F57-B8FA-CF5A6E1A8507}`.
- That DLL is the host for the desktop right-click entry ("AMD Software: Adrenalin Edition") and part of
  the hotkey handling. It has to live inside `explorer.exe` because that is how shell extensions work.
- `amdxc64.dll` / `atidxx64.dll` / `amdenc64.dll` in the same process are D3D/encoder UMds — every D3D
  program loads those, that part is normal.

**Consequence:** a fault in the shell extension takes `explorer.exe` down with it — the desktop flashes
and reloads. After restarting Explorer the AMD entry is gone until the Adrenalin service re-injects it.

**Correction to an earlier oversimplification:** the *panel itself* (`RadeonSoftware.exe`) is a separate
process. "The panel hangs on Explorer" is true for its **entry points** (context menu, hotkeys), not for
the whole application.

**Presets:** the overclocking presets are stored by the driver's profile mechanism and get overwritten or
cleared after driver updates, profile reloads, or panel re-initialisation — observed repeatedly on the
target machine.

RDNA Panel's answer is structural: native ADLX/ADL core, plain WPF UI, presets in files the tool owns.

## 2. Why there is no VRAM overclocking: a symptom AMD has not fixed since RDNA3

The starting point was **a symptom, not a theory**:

> Hot-changing the memory clock goes straight to a white screen / black screen — on RDNA2 or NVIDIA this
> almost never happens. It is **switching the clock under load** that kills it; idle and stress testing are
> fine. And the display state cannot be recovered without a reboot.


That question started the investigation. Two facts came out of it:

1. The failing moment is the **transition**, i.e. a **transient droop** problem, not a steady-state
   voltage deficit. The driver's voltage offset is **static**; it cannot change the droop curve.
2. The droop curve is governed by the **VRM load-line (RLL)**, together with IDROOP and TRIM behaviour.
   Crucially, RLL/droop is **not a driver-side setting**: it is a VRM-controller parameter reached over
   **I2C/SMBus** (or fixed in the board **BIOS**), which is why the graphics driver cannot expose or change it.

**External data point** — [hardwareluxx: Offizieller AMD RX 9070 (XT) Overclocking und Modding Thread, p.68](https://www.hardwareluxx.de/community/threads/offizieller-amd-rx-9070-xt-overclocking-und-modding-thread.1364991/page-68)
(post by user *RedF*; a single case, not reproduced here):

```
RLL 1.196 -> 1.296 (more droop, less overshoot)
Max Droop Compensation 25 mV -> 20 mV
Result: stable memory clock 2880 -> 2960 MHz
The 10 mV offset change (1.345 -> 1.335) was secondary; the droop tuning did the work.
```

**This is not a tuning gap that the tool can paper over.** It is an AMD-side issue that has persisted
from **RDNA3 through RDNA4 and remains unfixed**. The droop curve lives in the VRM (I2C register) or the
BIOS, not in the driver — so it cannot be corrected from an application at all. RDNA Panel therefore does **not** offer VRAM
overclocking, memory voltage or timing tuning: a feature that would look fine in a stress test and then
drop the display at a load change is worse than no feature.

**Severity, as observed on the target machine:** when it happens, **Windows' TDR does not fire**. There is
no driver reset, no recovery dialog, no automatic restore — the display simply stays dead. The only way
back is a **forced power cycle**, and in the bad cases **boot into Safe Mode and uninstall the driver**
before the card comes back. A failure mode that cannot be recovered from by the OS watchdog is the reason
this feature is not offered at all, not merely tuned conservatively.

If someone revisits this: the primary variable is RLL/droop, not the clock offset, and the experiment
needs a fallback display path (iGPU / second GPU) plus a recovery procedure.

## 3. Where these notes come from

- Panel architecture: analysis of how the vendor software is packaged (WebView2 + shell-hosted window),
  cross-checked against the behaviour observed on the target machine.
- Preset loss: repeated, user-visible behaviour on the target machine.
- VRAM/UCLK: architectural reasoning about how memory clock changes are executed; explicitly **not**
  a measurement, and not presented as one.

## 4. Memory timing presets (Default / Fast Timing / Level 1-2)

ADLX exposes six timing descriptions, but **AMD publishes no definition of what each one changes**:

- `ADLXDefines.h` only names them: `MEMORYTIMING_DEFAULT`, `FAST_TIMING`, `FAST_TIMING_LEVEL_2`,
  `AUTOMATIC`, `MEMORYTIMING_LEVEL_1`, `MEMORYTIMING_LEVEL_2`.
- AMD's own tuning KB (DH2-020, "How to Tune GPU Performance Using Radeon Software") lists *Memory
  Timing* as a tuning option but does not explain the levels.
- The ADLX sample only maps the enum values to display strings and enumerates whatever the driver
  reports via `GetSupportedMemoryTimingDescriptionList`.

Community measurements (RX 550, WattMan, 1900 MHz, OCL MemBench): Automatic 97.7 GB/s,
Level 1 103 GB/s, Level 2 104 GB/s — i.e. **higher level = tighter timings / more bandwidth**, and
Level 1/2 at 1900 MHz beat 1950 MHz at Automatic. The same article states AMD provides no complete
documentation of which timings change.

Sources:
- https://web.archive.org/web/20210209154909/https://www.amd.com/en/support/kb/faq/dh2-020
- https://oc.jagatreview.com/2019/01/tweaking-gpu-memory-timing-di-radeon-wattman-radeon-software-adrenalin-2019/2/

Because of that, RadeonTune **enumerates the driver's supported list** instead of hardcoding names, and on
RDNA3/RDNA4 the whole memory-timing section is absent (same policy as VRAM clock).
