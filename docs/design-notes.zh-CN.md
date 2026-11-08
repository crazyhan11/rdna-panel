# 设计说明与依据

这个文件记录 RDNA Panel 为什么是现在这个样子，以及哪些是**观察**、哪些是**分析**、哪些是**推理**。

标记：**观察** = 本机复现过；**分析** = 从其构建方式推断；**推理** = 合理但**未实测**。

## 1. 厂商面板：真正挂在 Explorer 上的是什么

**本机观察（`explorer.exe` 的模块列表）：**

- `amdihk64.dll` 被加载进 `explorer.exe`，注册名 **"Radeon Settings: Host Service"**，
  CLSID `{CCB5F028-A16A-4F57-B8FA-CF5A6E1A8507}`。
- 这个 DLL 是桌面右键菜单（"AMD Software: Adrenalin Edition"）以及部分热键功能的宿主。
  shell 扩展的机制决定了它必须跑在 `explorer.exe` 进程里。
- 同一进程里的 `amdxc64.dll` / `atidxx64.dll` / `amdenc64.dll` 是 D3D/编码 UMD，任何 D3D 程序都会加载，属正常。

**后果**：shell 扩展一出错，`explorer.exe` 会跟着崩，桌面闪一下重载；重启 Explorer 后 AMD 右键菜单
会消失，直到 Adrenalin 服务重新注入。

**对早前说法的更正**：面板本体（`RadeonSoftware.exe`）是独立进程。「面板挂在 explorer 上」
只对它的**入口**（右键菜单、热键）成立，不是整个面板。

**预设**：超频预设由驱动的 profile 机制保存，驱动更新、配置重载、面板重新初始化后会被覆盖或清除——
本机反复观察到。

RDNA Panel 的应对是结构性的：原生 ADLX/ADL 核心、纯 WPF 界面、自己持有的预设文件。

## 2. 为什么不做显存超频：一个从 RDNA3 到现在都没修的 AMD 问题

起点是**现象，不是理论**：

> 热修改显存频率会直接白屏/黑屏——这在 RDNA2 或 N 卡上几乎不会发生。真正致命的是
> **有负载时切换频率**；空闲与烤鸡反而不受影响。而且白屏之后无法热恢复，只能重启。


这个疑问才引出了后面的调研。调研得出两点：

1. 崩的瞬间是**切换**，也就是**瞬态掉压（transient droop）**，不是稳态电压不够。驱动侧电压偏移是**静态**的，改不了掉压曲线。
2. 掉压曲线由 **VRM 的 load-line（RLL）** 决定，另外还涉及 IDROOP 与 TRIM。关键在于：RLL/掉压
   **不是驱动侧设置**，而是通过 **I2C/SMBus** 访问的 VRM 控制器参数（或在主板 **BIOS** 里固定），
   所以驱动根本无法暴露或修改它。

**外部数据点** —— [hardwareluxx：Offizieller AMD RX 9070 (XT) Overclocking und Modding Thread 第 68 页](https://www.hardwareluxx.de/community/threads/offizieller-amd-rx-9070-xt-overclocking-und-modding-thread.1364991/page-68)
（用户 *RedF* 的帖子；单个案例，本项目未复现）：

```
RLL 1.196 -> 1.296（更多 droop、更少 overshoot）
Max Droop Compensation 25 mV -> 20 mV
结果：显存稳定频率 2880 -> 2960 MHz
他降的那 10 mV（1.345 -> 1.335）只是配菜，主角是把瞬态调平。
```

**这不是本工具能绕过的调参缺口。** 它是 AMD 侧的问题，从 **RDNA3 一直延续到 RDNA4，至今未修**。
掉压曲线在 VRM（I2C 寄存器）或 BIOS 里，不在驱动里——所以任何应用程序都无法修正它。因此 RDNA Panel **不提供**显存超频、
显存电压与时序调节：一个能过压力测试、却在负载切换时掉显示的功能，比没有更糟。

**严重程度（本机观察）**：一旦发生，**Windows 的 TDR 不会触发**——没有驱动重置、没有恢复提示、没有自动还原，
显示就是死的。唯一的恢复方式是**强制断电重启**；严重时还要**进安全模式把驱动卸掉**，显卡才回来。
一个连 OS 看门狗都救不回来的失效模式，是不提供该功能的理由，而不是“保守调参”的问题。

将来若有人重做：主变量是 RLL/掉压而不是频率偏移，并且必须准备回退显示路径（核显/第二张卡）
与恢复步骤。

## 3. 这些结论的来源

- 面板架构：对厂商软件打包方式（WebView2 + shell 宿主窗口）的分析，并与本机观察到的行为对照。
- 预设丢失：本机反复出现、用户可见的行为。
- 显存/UCLK：关于显存频率切换如何执行的架构推理；**明确不是测量**，也不当作测量来表述。

## 4. 显存时序预设（Default / Fast Timing / Level 1-2）

ADLX 暴露六种时序描述，但 **AMD 没有公开每一种具体改了什么**：

- `ADLXDefines.h` 只给了名字：`MEMORYTIMING_DEFAULT`、`FAST_TIMING`、`FAST_TIMING_LEVEL_2`、
  `AUTOMATIC`、`MEMORYTIMING_LEVEL_1`、`MEMORYTIMING_LEVEL_2`；
- AMD 官方调优文档（DH2-020《How to Tune GPU Performance Using Radeon Software》）把 *Memory Timing*
  列为可调项，但没有解释 level 的含义；
- ADLX 官方示例只是把枚举映射成字符串，并枚举驱动通过
  `GetSupportedMemoryTimingDescriptionList` 报上来的项。

社区实测（RX 550、WattMan、1900 MHz、OCL MemBench）：Automatic 97.7 GB/s、Level 1 103 GB/s、
Level 2 104 GB/s——即 **level 越高时脉越紧、带宽越大**，且 1900 MHz + Level 1/2 快过 1950 MHz + Automatic。
同一篇也写明 AMD 没有提供完整文档说明改了哪些时序。

来源：
- https://web.archive.org/web/20210209154909/https://www.amd.com/en/support/kb/faq/dh2-020
- https://oc.jagatreview.com/2019/01/tweaking-gpu-memory-timing-di-radeon-wattman-radeon-software-adrenalin-2019/2/

因此 RadeonTune **枚举驱动支持列表**而不是写死名称；RDNA3/RDNA4 下整个显存时序区不显示（与显存频率同一策略）。
