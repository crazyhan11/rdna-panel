# RDNA2（RX 6000）超频适配说明

## 实测环境

- GPU：AMD Radeon RX 6800 XT（Navi 21，DeviceId `73BF`）
- 驱动：32.0.21045.16010
- 驱动内置 ADLX：1.4（`C:\Windows\System32\amdadlx64.dll`）
- 编译工具：mingw-w64 g++ 16.2.0 + .NET 8 SDK（均放在 `E:\rdna\tools`）

## 结论：6000 暴露的接口

原 README 写“RDNA2/3 走同一套 ADLX 接口”。实测确认，但有两个关键差异：

1. **接口版本**
   - ADLX 的 `IADLXManualGraphicsTuning1` 是 pre-Navi（Vega/Polaris）的 V/F 状态表接口。
   - Navi 及以后（RDNA1~4）走 `IADLXManualGraphicsTuning2`。
   - RX 6800 XT 上 `QueryInterface(v1)` 返回失败，`v2` / `v2_1` 成功。即 6000 并没有“另一套接口”，而是**没有 v1，只有 v2**。

2. **同一套 v2 接口的语义不同**
   - RDNA4（Navi4）：`GetGPUMaxFrequency` / `GetGPUVoltage` 返回的是**相对默认值的偏移**。
   - RDNA2（Navi2）：同一对方法返回的是**绝对值**（MHz / mV）。
   - RDNA2 额外支持**最低频率**（`GetGPUMinFrequencyRange/GetGPUMinFrequency/SetGPUMinFrequency`），Adrenalin 的 6000 手动调优里有 Min Frequency；RDNA4 已取消这一项。

实测读数（通过 ADLX）：

```
v2 maxFreq range 500..2800  cur 2450
v2 volt    range 881..1150  cur 1075
v2 minFreq range 500..2800  cur 500
v2_1 defaults: max 2404, volt 1150, min 500
power limit -15..15 %  cur 0
fan 30..100 %，5 点曲线，ZeroRPM 支持
```

## 原实现的问题

原 `rt_core.cpp` 只有 `getGfx2()`，本身能在 6000 上读写，但上层 UI 全是 RDNA4 假设：

- `MainWindow` 用 `3450 + offset` 计算频率；RDNA2 回读已是 2450 MHz 绝对值，界面会显示 5900 MHz。
- 首页/滑杆把电压写成“电压偏移”；RDNA2 的 1075 是绝对 mV。
- 没有最低频率滑杆。
- `SaveCurrentProfile` 不保存电压；在 RDNA2 上应用这种配置会把电压写成 0 mV。
- 预设文本、APPLY 结果、频率预测区间都按偏移格式化。

## 修改内容

### native/rt_core.cpp

- 新增 `OcCaps` 与 `queryOcCaps()`：
  - 通过 `IADLXManualGraphicsTuning2_1` 的默认值判断语义：
    - 默认 max 频率 > 300 MHz → 绝对值（RDNA2）；否则偏移（RDNA4）。
    - 默认电压 > 300 mV → 绝对值；否则偏移。
    - 默认值不可用时退回量程判断（`minValue >= 300`）。
  - 探测最低频率量程与当前值。
- 新增导出：
  - `rt_oc2_caps(buf,len)` → `v2|v1|freq_abs|volt_abs|minfreq_sup|default_maxfreq|default_volt`
  - `rt_oc2_min(buf,len)` → `min|max|cur`
  - `rt_oc2_set_min(mhz)`
- `rt_od8_limits` 改为输出完整可调范围（频率上限/最低频率/功耗/电压、语义标志），原 `freqmin` 等键保留。
- `rt_dbg` 追加语义探测结果，便于现场排障。

### RtCore.cs

新增 `rt_oc2_caps`、`rt_oc2_min`、`rt_oc2_set_min` 的 P/Invoke。

### Engine.cs

- `Profile` / `HwState` 增加 `MinFreq` / `HasMinFreq`。
- `CaptureHw`、`RestoreRollback`、`Apply` 支持最低频率的写入与回退。
- 应用失败时一并回退最低频率。

### MainWindow.xaml.cs / MainWindow.xaml

- 启动时读取 `rt_oc2_caps`，按能力渲染：
  - 绝对值模式：标题“核心频率上限（MHz）”“核心电压（mV）”，频率直接显示 `2450 MHz`。
  - 偏移模式：保持原 RDNA4 文案与 `3450 + offset` 显示。
  - 支持最低频率时插入“最低频率（MHz）”滑杆。
- 首页大数字、预设列表、APPLY 结果、频率预测区间、驱动范围区全部按语义分支。
- 切换显卡/切换语言重建超频页后，重新回读滑块值（`RefreshOcValues`）。
- `SaveCurrentProfile` 补存电压与最低频率。

## 构建

```powershell
# 原生核心（ADLX 头文件不随仓库分发，这里用官方 SDK 仓库）
powershell -ExecutionPolicy Bypass -File native\build.ps1 `
  -Adlx E:\rdna\ADLX-main -Gxx E:\rdna\tools\mingw64\bin\g++.exe

# C# 面板
E:\rdna\tools\dotnet\dotnet.exe build RadeonTune.csproj -c Release
```

## 验证

1. 原生导出只读探测（RX 6800 XT）：

```
rt_oc2_caps : rc=0 1|0|1|1|1|2404|1150
rt_oc2_info : rc=0 500|2800|2450|-15|15|0
rt_oc2_volt : rc=0 881|1150|1075
rt_oc2_min  : rc=0 500|2800|500
rt_dbg      : ... v1=0 fabs=1 vabs=1 minsup=1 defmax=2404 defvolt=1150
```

2. 写入闭环（写入当前值，随后回退）：

```
apply : freq=2450 min=500 volt=1075 power=0
result: ok=True
readback oc  : 500|2800|2450|-15|15|0
readback volt: 881|1150|1075
readback minf: 500|2800|500
rollback     : 已回退
```

`SetGPUMinFrequency` / `SetGPUMaxFrequency` / `SetGPUVoltage` 在 6000 上均返回 `ADLX_OK`，不需要先 `ResetToFactory`。

## 打包为安装程序

安装包用 NSIS 3.13 生成（路径 `packaging\nsis\installer.nsi`）：

```powershell
# 1) 自包含发布到 packaging/portable
E:\rdna\tools\dotnet\dotnet.exe publish RadeonTune.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=false -o packaging\portable

# 2) 编译安装包
cd packaging\nsis
E:\rdna\tools\nsis-3.13\makensis.exe installer.nsi
```

产物：`RDNAPanel-Setup-1.3.0-rdna2.exe`（单文件，约 70 MB，内含 .NET 8 运行时）。

行为：

- 按用户安装到 `%LOCALAPPDATA%\Programs\RDNAPanel`，不需要管理员权限（ADLX 写调优时程序内再提权）。
- 创建开始菜单/桌面快捷方式，写入 HKCU 卸载项。
- 覆盖安装会保留 `data` 配置目录。
- 卸载会删除安装目录（含 `data`）、快捷方式和注册表项。

已在 RX 6800 XT 上实测：静默安装 → 校验 `rt_core.dll` → `RDNAPanel.exe --octest`（写入 +5 MHz/+5 mV 并恢复）→ 静默卸载，目录、快捷方式、注册表均清理干净。

## 兼容性

- RDNA4 仍走偏移分支，行为与原版一致。
- RDNA1/3 若驱动默认值落在绝对量程，会自动按绝对值显示；否则按偏移。
- pre-Navi（只有 v1）仍不支持，`rt_oc2_*` 返回 -2，与原版一致。
- 只读取/写入 `E:\rdna` 下的源码与构建产物；调优写入仍走 ADLX 驱动接口。
