# RDNA Panel

**English** | [简体中文](#简体中文)

---

<a id="english"></a>

## English

**A lightweight tuning & monitoring panel for AMD Radeon GPUs (RDNA4 / RX 9070 tested), built on ADLX/ADL.**

RDNA Panel is a compact WPF (.NET 8) control panel for AMD Radeon graphics cards. It talks to the driver
through a small native core (`rt_core.dll`, ADLX/ADL) and exposes the same controls the vendor panel has, without its shell/WebView stack —
overclocking offsets, fan curve, display modes, 3D feature switches, and live multi-GPU monitoring.
None of these are secret switches: they are the same controls the vendor panel offers — the difference is
that they live in a normal desktop app instead of a shell-hosted WebView.

## Screenshots

<table>
<tr>
  <td width="50%"><img src="docs/home.png" alt="Home / monitoring"><br><sub><b>Home</b> — per-GPU panels, one row per card</sub></td>
  <td width="50%"><img src="docs/graphics.png" alt="Graphics settings"><br><sub><b>Graphics</b> — Anti-Lag, Boost, RSR, FSR, AFMF</sub></td>
</tr>
<tr>
  <td><img src="docs/display.png" alt="Display settings"><br><sub><b>Display</b> — resolution, refresh rate, bit depth, VRR</sub></td>
  <td><img src="docs/overclocking.png" alt="Overclocking"><br><sub><b>Overclocking</b> — clock limit, power limit, voltage offset, fan curve</sub></td>
</tr>
<tr>
  <td><img src="docs/profiles.png" alt="Profiles"><br><sub><b>Profiles</b> — per-game profiles, autostart, close behaviour, language</sub></td>
  <td></td>
</tr>
</table>

## Why this exists

RDNA Panel is **not** an overclocking toy — it is a replacement for the vendor's driver control panel,
because that panel is unreliable by construction:

- It is a **browser-based front end hosted inside Explorer**. When Explorer restarts, hangs, or the
  WebView stack misbehaves, the panel disappears, goes blank, or refuses to reopen.
- Its **overclocking presets are stored by the driver's own mechanism**, and that storage gets
  silently overwritten or cleared — after driver updates, profile reloads, or when the panel itself
  re-initialises. Tuned values simply vanish.

RDNA Panel avoids both failure modes (see [docs/design-notes.md](docs/design-notes.md) for the full
analysis and what is observed vs. inferred): a small native core (`rt_core.dll`) talks to ADLX/ADL directly,
the UI is plain WPF with no WebView and no Explorer dependency, and presets live in your own files
(`data\presets.json`, `data\settings.json`) that nothing else rewrites.

## What it deliberately does not do

**No VRAM overclocking, no memory voltage or timing tuning.** A memory clock change on these GPUs
forces a UCLK switch, and combined with the driver's anti-droop (LLC) regulation that is a classic
source of display-link instability (black/white screens).

**This is a known AMD issue, unfixed from RDNA3 through RDNA4:** a memory overclock passes stress tests
and then drops the display at idle ↔ load transitions. Load-line / droop lives in the VRM (I2C register) or the
BIOS, not in the driver, so no application can correct it. It does not even trigger TDR — recovery means a forced power cycle,
sometimes Safe Mode plus a driver uninstall. That is why the feature is absent rather than unfinished.
See [docs/design-notes.md](docs/design-notes.md) for the full reasoning and the status of each claim.

## Recommended setup

Install the AMD driver with the **"Driver Only"** option (skip the bundled control panel). You get the
kernel driver and ADLX, but not the unstable WebView panel — RDNA Panel provides the controls you
actually use, and the machine stays free of the panel's Explorer coupling.

## Features

- **Live monitoring** — core/VRAM clock, edge & hotspot temperature, VRAM temperature, board power,
  GPU voltage, fan RPM, GFX activity. One full panel per GPU; multi-GPU systems show every card.
- **Multi-GPU** — pick the active card with one click; the choice is remembered. Monitoring of idle
  cards can be disabled to keep CPU package C-states intact.
- **Overclocking** — core clock limit, power limit, voltage offset, 5-point fan curve, zero-RPM,
  save/apply presets, apply-at-startup, one-click revert.
- **Display** — resolution & refresh rate switching with a 15-second confirm/rollback guard,
  color depth, pixel format, GPU scaling, VSR, custom resolutions.
- **3D / graphics** — Anti-Lag, Boost, Chill, Enhanced Sync, FRTC, RSR upscaling & sharpness,
  Image Sharpening 2, FSR upscaling / frame generation, AFMF switches.
- **Profiles** — global and per-game profiles, process-based auto-apply, cover art.
- **Quality of life** — tray icon (always resident), close-to-tray with rememberable choice,
  start with Windows (with configurable delay), single instance, Chinese/English UI.

## Install

### Installer (recommended)

1. Download `RDNA Panel-Setup-1.2.6.exe` and run it.
2. The installer is per-user (no admin), adds Start Menu / Desktop shortcuts and an uninstall entry.
3. It installs to `%LOCALAPPDATA%\Programs\RDNA Panel`, creates Start Menu / Desktop shortcuts and an
   uninstall entry (Apps & Features). No admin rights required.
4. To remove: run `uninstall.ps1` from the install folder, or use Apps & Features.

### Portable

1. Download `RDNA Panel-v1.2.6-portable-win-x64.zip` and extract anywhere.
2. Run `RDNA Panel.exe`. Settings live in `data\settings.json` next to the executable.

Requirements: Windows 10/11 x64, an AMD Radeon GPU with a recent Adrenalin driver.
Self-contained build — no .NET runtime installation needed.

## Usage notes

- Overclocking writes go through ADLX; values are validated against driver limits. Use at your own risk.
- Display mode changes show a confirmation dialog and roll back automatically after 15 s.
- Multi-GPU: the selected card is used for tuning pages; monitoring can cover all cards.
- Language can be switched at any time in **Profiles → Language**.

## Build from source

```
dotnet publish RDNA Panel/RDNA Panel.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=false -o RDNA Panel/dist
```

The native core `rt_core.dll` is built from `RDNA Panel/native/rt_core.cpp` with mingw-w64
(ADLX SDK headers required and not bundled):

```
g++ -std=c++17 -O2 -shared -static -I <ADLX>/SDK/Include -I <ADLX>/SDK/ADLXHelper/Windows/Cpp `
  native/rt_core.cpp <ADLX>/SDK/ADLXHelper/Windows/Cpp/ADLXHelper.cpp `
  <ADLX>/SDK/Platform/Windows/WinAPIs.cpp -o rt_core.dll -ladvapi32
```

Or use the helper script:

```
powershell -ExecutionPolicy Bypass -File native/build.ps1 -Adlx <ADLX SDK root>
```

## Status

Early but functional. Verified on Radeon RX 9070 (Navi 48) + iGPU. RDNA2 (RX 6000) is now adapted and
tested on RX 6800 XT: the same `IADLXManualGraphicsTuning2` interface is used, but clock/voltage are absolute
values (not RDNA4 offsets) and minimum frequency is exposed. See
[docs/rdna2-adaptation.md](docs/rdna2-adaptation.md).

## License

MIT — see [LICENSE](LICENSE).

---

<a id="简体中文"></a>

## 简体中文

[English](#english) | **简体中文**


**面向 AMD Radeon 显卡（RDNA4 / 已在 RX 9070 验证）的轻量调优与监控面板，基于 ADLX/ADL。**

RDNA Panel 是一个紧凑的 WPF（.NET 8）控制面板，通过原生核心 `rt_core.dll`（ADLX/ADL）直接与驱动交互，
把官方面板里同样的控制项集中到一处（没有隐藏开关）：超频偏移、风扇曲线、显示模式、3D 功能开关，以及多卡实时监控。这些都不是隐藏开关——
它们与官方面板提供的控制项相同，区别只在于它们待在一个普通桌面程序里，而不是 shell 宿主的 WebView 里。

## 为什么做这个

RDNA Panel **不是**超频玩具，而是驱动控制面板的替代品——因为那个面板在设计上就不稳定：

- 它是**依托 Explorer 的浏览器前端**。Explorer 重启、卡住，或 WebView 栈出问题，面板就消失、白屏，
  甚至再也打不开。
- 它的**超频预设由驱动自己的机制保存**，而这个存储会被静默覆盖或清除——驱动更新、配置重载、
  面板自身重新初始化之后，调好的数值就没了。

RDNA Panel 绕开这两个失效点（完整分析与「观察/推理」的区分见
[docs/design-notes.zh-CN.md](docs/design-notes.zh-CN.md)）：原生核心 `rt_core.dll` 直接调用 ADLX/ADL，界面是纯 WPF，
不依赖 WebView、不依赖 Explorer；预设保存在自己的文件里（`data\presets.json`、`data\settings.json`），
没有别的程序会去改写它们。

## 刻意不做的事

**不提供显存超频，也不做显存电压/时序调节。** 这类显卡上改显存频率会触发 UCLK 切换，
再叠加驱动的防掉压（LLC）调节，是显示链路失稳（黑屏/白屏）的典型来源。

**这是 AMD 侧从 RDNA3 到 RDNA4 都没修的问题**：显存超频能过压力测试，却会在空载↔负载切换时掉显示。
驱动不暴露 load-line / 掉压控制，用户态无法修正；它甚至连 TDR 都触发不了，恢复只能强制断电重启，
严重时要进安全模式卸驱动——所以这个功能是「做不了」，不是「没做完」。完整推理与每条结论的状态见
[docs/design-notes.zh-CN.md](docs/design-notes.zh-CN.md)。

## 推荐安装方式

安装 AMD 驱动时选择 **「仅驱动」（Driver Only）**，跳过随附的控制面板。这样会装上内核驱动与 ADLX，
但不装那个不稳定的 WebView 面板——日常需要的控制项由 RDNA Panel 提供，系统也不再被面板和 Explorer 绑在一起。

## 功能

- **实时监控** —— 核心/显存频率、边缘与热点温度、显存温度、整卡功耗、GPU 电压、风扇转速、GFX 活动；
  每张显卡一块完整面板，多卡系统会同时展示所有卡。
- **多显卡** —— 一键切换当前操作的显卡并记住选择；可关闭空闲卡的轮询，避免影响 CPU 包级 C-state。
- **超频** —— 核心频率上限、功耗上限、电压偏移、5 点风扇曲线、低温零转速、预设保存与应用、
  开机自动应用、一键回退。
- **显示** —— 分辨率与刷新率切换（15 秒确认/自动回滚保护）、位深、像素格式、GPU 缩放、VSR、自定义分辨率。
- **图形设置** —— Anti-Lag、Boost、Chill、Enhanced Sync、FRTC、RSR 超分与锐化、Image Sharpening 2、
  FSR 超分/帧生成、AFMF 开关。
- **配置** —— 全局与单游戏配置、按进程自动应用、封面图。
- **易用性** —— 托盘常驻、关闭到托盘并可记住选择、开机自启（可设延迟）、单实例、中英文界面。

## 安装

### 安装版（推荐）

1. 下载 `RDNA Panel-Setup-1.2.6.exe` 直接运行；
2. 安装到当前用户（无需管理员），自动创建开始菜单/桌面快捷方式与卸载项；
3. 安装到 `%LOCALAPPDATA%\Programs\RDNA Panel`，创建开始菜单/桌面快捷方式与卸载项，无需管理员权限；
4. 卸载：运行安装目录里的 `uninstall.ps1`，或在“应用和功能”里卸载。

### 便携版

1. 下载 `RDNA Panel-v1.2.6-portable-win-x64.zip` 解压到任意目录；
2. 运行 `RDNA Panel.exe`，配置保存在同目录 `data\settings.json`。

环境要求：Windows 10/11 x64、AMD Radeon 显卡 + 较新的 Adrenalin 驱动。自带运行时，无需另装 .NET。

## 使用说明

- 超频写入走 ADLX，并按驱动上限校验；请自行承担风险。
- 切换显示模式会弹出确认框，15 秒内不确认自动回滚。
- 多显卡：调优页作用于所选显卡；监控可覆盖全部显卡。
- 语言可在 **配置 → 语言** 中随时切换。

## 从源码构建

```
dotnet publish RDNA Panel/RDNA Panel.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=false -o RDNA Panel/dist
```

原生核心 `rt_core.dll` 由 `RDNA Panel/native/rt_core.cpp` 用 mingw-w64 编译（需要 ADLX SDK 头文件，仓库未附带）：

```
g++ -std=c++17 -O2 -shared -static -I <ADLX>/SDK/Include -I <ADLX>/SDK/ADLXHelper/Windows/Cpp `
  native/rt_core.cpp <ADLX>/SDK/ADLXHelper/Windows/Cpp/ADLXHelper.cpp `
  <ADLX>/SDK/Platform/Windows/WinAPIs.cpp -o rt_core.dll -ladvapi32
```

或使用脚本：

```
powershell -ExecutionPolicy Bypass -File native/build.ps1 -Adlx <ADLX SDK 根目录>
```

## 状态

早期但可用。已在 Radeon RX 9070（Navi 48）+ 核显上验证。RDNA2（RX 6000）已完成适配，
在 RX 6800 XT 上实测：同样走 `IADLXManualGraphicsTuning2`，但频率/电压是绝对值（不是 RDNA4 的偏移），
并额外暴露最低频率。详见 [docs/rdna2-adaptation.md](docs/rdna2-adaptation.md)。

## 许可证

MIT，见 [LICENSE](LICENSE)。
