using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RadeonTune
{
    /// <summary>极简本地化：中文原文为键，英文为值；未收录的字符串原样显示。</summary>
    public static class Loc
    {
        public static string Language = "zh";

        private static readonly Dictionary<string, string> En = new()
        {
            // 侧栏
            ["首页"] = "Home", ["图形设置"] = "Graphics", ["全局"] = "Global", ["游戏"] = "Games",
            ["显示设置"] = "Display", ["基本"] = "Basic", ["自定义"] = "Custom", ["缩放"] = "Scaling",
            ["硬件"] = "Hardware", ["超频"] = "Overclocking", ["配置"] = "Profiles",
            // 通用
            ["保存"] = "Save", ["应用"] = "Apply", ["删除"] = "Delete", ["加载"] = "Load",
            ["开机"] = "Auto", ["取消"] = "Cancel", ["确定"] = "OK", ["退出程序"] = "Exit",
            ["最小化到托盘"] = "Minimize to tray", ["每次询问"] = "Ask every time",
            ["退出"] = "Exit", ["启用"] = "Enable", ["延迟（秒）"] = "Delay (s)",
            // 首页
            ["核心频率"] = "Core clock", ["电压偏移"] = "Voltage offset", ["边缘温度"] = "Edge temp",
            ["热点温度"] = "Hotspot temp", ["整卡功耗"] = "Board power", ["风扇"] = "Fan",
            ["显存频率"] = "VRAM clock", ["显存温度"] = "VRAM temp", ["GPU 电压"] = "GPU voltage",
            ["风扇转速"] = "Fan RPM", ["GFX 活动"] = "GFX activity",
            // 图形
            ["响应与延迟"] = "Response & latency", ["画质增强"] = "Image quality",
            ["帧生成"] = "Frame generation", ["同步与功耗"] = "Sync & power",
            ["Radeon Anti-Lag"] = "Radeon Anti-Lag", ["Radeon Boost"] = "Radeon Boost",
            ["Radeon Chill"] = "Radeon Chill", ["Radeon Super Resolution"] = "Radeon Super Resolution",
            ["AMD FSR 超分辨率"] = "AMD FSR upscaling", ["AMD FSR 帧生成"] = "AMD FSR frame gen",
            ["AMD Fluid Motion Frames"] = "AMD Fluid Motion Frames",
            ["Image Sharpening 2"] = "Image Sharpening 2", ["RSR 锐化"] = "RSR sharpness",
            ["Radeon Enhanced Sync"] = "Radeon Enhanced Sync",
            ["Frame Rate Target Control"] = "Frame Rate Target Control",
            ["FreeSync / VRR"] = "FreeSync / VRR", ["AFMF 算法"] = "AFMF algorithm",
            ["AFMF 搜索模式"] = "AFMF search mode", ["自动"] = "Auto", ["增强"] = "Enhanced",
            ["标准"] = "Standard",
            // 显示
            ["分辨率"] = "Resolution", ["刷新率"] = "Refresh rate", ["位深"] = "Color depth",
            ["像素格式"] = "Pixel format", ["色域白点"] = "Gamut white point",
            ["VRR 状态"] = "VRR status", ["能力别名"] = "Capability aliases",
            ["VRR 等级（EDID 推断）"] = "VRR tier (EDID inference)",
            ["核心频率上限（偏移，相对 3450 MHz 默认）"] = "Core clock limit (offset vs 3450 MHz default)",
            ["核心频率上限（MHz）"] = "Core clock limit (MHz)",
            ["核心电压（mV）"] = "Core voltage (mV)",
            ["最低频率（MHz）"] = "Min clock (MHz)",
            ["核心频率上限"] = "Core clock limit",
            ["驱动可调范围（ADLX 只读）"] = "Driver limits (ADLX, read-only)",
            ["功耗上限（%）"] = "Power limit (%)",
            ["风扇曲线（X 温度 °C · Y 转速 %）"] = "Fan curve (X temp °C · Y speed %)",
            ["本卡不支持「最低声学限制 / 最低风扇转速」这两项（ADLX 报告为 unsupported）"] =
                "This card does not support Min Acoustic Limit / Min Fan Speed (ADLX reports unsupported)",
            ["当前使用"] = "active",
            // 运行时拼接消息（前缀）
            ["已应用预设 "] = "Applied preset ",
            ["已保存并应用 "] = "Saved and applied ",
            ["已保存预设 "] = "Saved preset ",
            ["已加载预设 "] = "Loaded preset ",
            ["已删除预设 "] = "Deleted preset ",
            ["未找到预设 "] = "Preset not found ",
            ["已切换到 "] = "Switched to ",
            ["应用失败："] = "Apply failed: ",
            ["切换失败"] = "Switch failed",
            ["已回退到 "] = "Reverted to ",
            ["没有可回退的快照"] = "No rollback snapshot",
            ["已设置封面"] = "Cover set",
            ["已更新封面："] = "Cover updated: ",
            ["已保存封面副本："] = "Cover copy saved: ",
            ["已启用：登录后 "] = "Enabled: at logon after ",
            ["已关闭登录自启"] = "Autostart disabled",
            ["当前：已启用（登录时启动）"] = "Current: enabled at logon",
            ["当前：未启用"] = "Current: disabled",
            ["获取失败"] = "Fetch failed",
            ["启用"] = "Enabled",
            ["（只读）"] = " (read-only)",
            ["已识别为 VRR 显示器，当前已启用"] = "VRR-capable display detected, currently enabled",
            ["已识别为 VRR 显示器，当前未启用"] = "VRR-capable display detected, currently disabled",
            ["不支持"] = "Not supported",
            // 说明/注释（悬停提示与旁注）
            ["把鼠标移到任意选项上，这里显示详细说明"] = "Hover any option to see details here",
            ["从驱动枚举到的模式中选择。切换后 15 秒内不确认会自动恢复原模式。"] =
                "Pick a mode enumerated from the driver. If you do not confirm within 15 s, the previous mode is restored.",
            ["以更高分辨率渲染再缩小到屏幕，提升细节，代价是性能。"] =
                "Render above native resolution and downscale for extra detail, at the cost of performance.",
            ["像素格式切换失败 rc={rc}（该格式可能不受支持）"] =
                "Failed to switch pixel format rc={rc} (this format may be unsupported)",
            ["显示器当前报告的白点色温，只读。"] = "White point currently reported by the display (read-only).",
            ["未检测到 FSR 超分运行时（可能不支持或使用驱动覆盖）"] =
                "No FSR upscaling runtime detected (unsupported, or overridden by the driver)",
            ["未检测到帧生成运行时（FSR 帧生成可能不可用）"] =
                "No frame generation runtime detected (FSR frame gen may be unavailable)",
            ["点击新建：只填写分辨率与刷新率，其余时序由 EDID 与所选时序标准自动计算。"] =
                "Click New: enter resolution and refresh rate only; timings are derived from EDID and the selected standard.",
            ["由 ADLX 报告的 VRR 支持与启用状态。"] = "VRR capability and state as reported by ADLX.",
            ["由显卡执行缩放（关闭时交给显示器处理），影响非原生分辨率的画质。"] =
                "Scale on the GPU (otherwise the display does it); affects non-native image quality.",
            ["让显示器刷新率跟随游戏帧率以消除撕裂；需要显示器 OSD 里也开启。"] =
                "Match refresh rate to frame rate to remove tearing; also enable it in the monitor OSD.",


            ["其他显卡"] = "Other GPUs",
            ["频率"] = "Clock",
            ["关闭按钮行为"] = "Close button behavior",

            ["缩放模式"] = "Scaling mode", ["GPU 缩放"] = "GPU scaling",
            ["保持纵横比"] = "Preserve aspect ratio", ["全屏"] = "Full panel", ["居中"] = "Centered",
            ["Custom resolution与刷新率"] = "Custom resolution & refresh rate",
            ["超虚拟分辨率（VSR）"] = "Virtual Super Resolution (VSR)",
            ["尚未创建（点击新建）"] = "Not created yet (click New)",
            ["点击新建"] = "New",
            ["自定义分辨率与刷新率"] = "Custom resolution & refresh rate",
            ["超虚拟分辨率 (VSR)"] = "Virtual Super Resolution (VSR)",
            ["禁用"] = "Disabled", ["超级虚拟分辨率 (VSR)"] = "Virtual Super Resolution (VSR)",
            ["自定义分辨率"] = "Custom resolution",
            // 超频
            ["核心频率上限"] = "Core clock limit", ["功耗上限"] = "Power limit",
            ["电压偏移（mV）"] = "Voltage offset (mV)", ["风扇曲线"] = "Fan curve",
            ["低温零转速（风扇停转）"] = "Zero RPM at low temperature",
            ["超频预设（保存 / 应用）"] = "Presets (save / apply)",
            ["保存当前为预设"] = "Save current as preset", ["保存后立即应用"] = "Apply after saving",
            ["开机自动应用"] = "Apply at startup", ["新建预设"] = "New preset",
            ["频率预测区间"] = "Predicted clock range",
            ["风扇曲线"] = "Fan curve", ["本卡不支持"] = "Not supported on this card",
            ["ADLX 范围"] = "ADLX range", ["理想轻负载（桌面 / 轻游戏）"] = "Light load (desktop / casual)",
            ["重负载（3DMark 类）"] = "Heavy load (3DMark)", ["极重算力负载（FSR 上采样 + 光追）"] = "Extreme load (FSR + RT)",
            ["当前实测（1 秒刷新）"] = "Live (1s refresh)",
            ["回退到上次应用前"] = "Revert last apply",
            // 配置
            ["开机自启（登录后启动本程序并最小化到托盘）"] = "Start with Windows (launch minimized to tray)",
            ["关闭按钮行为"] = "Close button behavior",
            ["语言"] = "Language", ["中文"] = "Chinese", ["English"] = "English",
            ["游戏库"] = "Game library", ["扫描游戏"] = "Scan games", ["添加文件夹"] = "Add folder",
            ["绑定进程（exe 名，不含 .exe）"] = "Bind process (exe name, no .exe)",
            ["配置名"] = "Profile name", ["设置封面"] = "Set cover",
            ["自动获取封面"] = "Auto-fetch cover", ["重置此游戏"] = "Reset this game",
            ["ADLX 初始化失败："] = "ADLX init failed: ",
            ["ADLX 范围 {vv[0]} … {vv[1]} mV"] = "ADLX range {vv[0]} … {vv[1]} mV",
            ["AFMF 算法：增强偏画质、标准偏性能，自动由驱动决定。"] = "AFMF algorithm: Enhanced favours quality, Standard favours performance; Auto lets the driver decide.",
            ["D65 ({gf[1]}/{gf[2]})（只读）"] = "D65 ({gf[1]}/{gf[2]}) (read-only)",
            ["EDID 无范围限制描述符"] = "EDID has no range-limit descriptor",
            ["EDID 解析失败"] = "EDID parse failed",
            ["GPU 缩放切换失败 rc={rc}"] = "GPU scaling switch failed rc={rc}",
            ["RSR 锐化写入失败 rc={rc}"] = "Failed to write RSR sharpness rc={rc}",
            ["RSR 锐化读取失败 rc="] = "Failed to read RSR sharpness rc=",
            ["V/F 分区电压偏移上限"] = "V/F curve voltage offset (upper)",
            ["V/F 分区电压偏移下限"] = "V/F curve voltage offset (lower)",
            ["VSR 切换失败 rc={rc}"] = "VSR switch failed rc={rc}",
            ["game:<exe路径>"] = "game:<exe path>",
            ["位深切换失败 rc={rc}"] = "Color depth switch failed rc={rc}",
            ["作用域"] = "Scope",
            ["作用域：全局（修改立即生效）"] = "Scope: Global (changes apply immediately)",
            ["保存并获取"] = "Save & read back",
            ["像素格式已切换：{val}"] = "Pixel format switched: {val}",
            ["关闭 RDNA Panel"] = "Close RDNA Panel",
            ["创建失败 rc={rc}（时序参数不被驱动接受）"] = "Create failed rc={rc} (timings rejected by the driver)",
            ["删除失败 rc={rc}"] = "Delete failed rc={rc}",
            ["功耗上限允许"] = "Power limit allowed",
            ["同时轮询其他显卡（影响 CPU C-state）"] = "Also poll other GPUs (affects CPU C-state)",
            ["名称 {name} 已存在"] = "Name {name} already exists",
            ["图形"] = "Graphics",
            ["尚无超频预设"] = "No OC presets yet",
            ["已保存并应用 {name}"] = "Saved and applied {name}",
            ["已保存配置 {name}"] = "Saved profile {name}",
            ["已保存预设 {name}"] = "Saved preset {name}",
            ["已删除 {label}"] = "Deleted {label}",
            ["已删除配置 {name}"] = "Deleted profile {name}",
            ["已删除预设 {pr.Name}"] = "Deleted preset {pr.Name}",
            ["已取消开机自动应用"] = "Cancelled apply-at-startup",
            ["已启用"] = "Enabled",
            ["已应用 {name}（回读一致）"] = "Applied {name} (read-back matches)",
            ["已应用预设 {pr.Name}"] = "Applied preset {pr.Name}",
            ["已恢复 {bw}×{bh} @ {bhz} Hz"] = "Restored {bw}×{bh} @ {bhz} Hz",
            ["已恢复全局默认（全部关闭）"] = "Restored global defaults (all off)",
            ["已扫描到 {games.Count} 个游戏"] = "Found {games.Count} games",
            ["已把 {name} 绑定到进程 {p.Process}"] = "Bound {name} to process {p.Process}",
            ["已按进程 {name} 应用 {prof.Name}"] = "Applied {prof.Name} for process {name}",
            ["已检测到 AMD FidelityFX 运行时："] = "Detected AMD FidelityFX runtime: ",
            ["已添加：{g.Name}"] = "Added: {g.Name}",
            ["已禁用"] = "Disabled",
            ["已获取封面："] = "Cover fetched: ",
            ["已解除 {name} 的进程绑定"] = "Unbound process from {name}",
            ["已设为开机自动应用：{pr.Name}"] = "Set as apply-at-startup: {pr.Name}",
            ["已重置该游戏配置，所有选项继承全局"] = "Game profile reset; all options now inherit Global",
            ["应用失败：{ar.Detail}"] = "Apply failed: {ar.Detail}",
            ["应用失败：{r.Detail}"] = "Apply failed: {r.Detail}",
            ["延迟需为 0–3600 秒之间的整数"] = "Delay must be an integer between 0 and 3600 s",
            ["开机✓"] = "Startup ✓",
            ["当前：未注册"] = "Current: not registered",
            ["恢复全局默认"] = "Restore global defaults",
            ["恢复失败："] = "Restore failed: ",
            ["提权失败："] = "Elevation failed: ",
            ["搜索游戏"] = "Search games",
            ["无法初始化 ADLX（"] = "Cannot initialize ADLX (",
            ["无法读取 EDID"] = "Cannot read EDID",
            ["显存频率范围"] = "VRAM clock range",
            ["显示"] = "Display",
            ["显示 · "] = "Display · ",
            ["显示页读取失败："] = "Failed to read display page: ",
            ["最低声学限制：{cur} MHz（{lo}–{hi}）"] = "Min acoustic limit: {cur} MHz ({lo}–{hi})",
            ["最低风扇转速：{cur} %（{lo}–{hi}）"] = "Min fan speed: {cur} % ({lo}–{hi})",
            ["未找到该游戏记录"] = "Game record not found",
            ["未找到预设 {pr.Name}（键不匹配）"] = "Preset {pr.Name} not found (key mismatch)",
            ["未知"] = "Unknown",
            ["核心频率范围"] = "Core clock range",
            ["检测到 {name}，应用配置 {prof.Name}…"] = "Detected {name}, applying {prof.Name}…",
            ["正在从 SteamGridDB 获取封面…"] = "Fetching cover from SteamGridDB…",
            ["正在写入 {key}…"] = "Writing {key}…",
            ["正在写入超频设置…"] = "Writing overclocking settings…",
            ["正在应用 {name}…"] = "Applying {name}…",
            ["没有选中游戏，无法设置封面"] = "No game selected; cannot set cover",
            ["游戏 · "] = "Games · ",
            ["游戏 › "] = "Games › ",
            ["电压 {kv.Value.VoltMv} mV"] = "Voltage {kv.Value.VoltMv} mV",
            ["绑定 "] = "Bind ",
            ["缩放模式切换失败 rc={rc}"] = "Scaling mode switch failed rc={rc}",
            ["自定义曲线"] = "Custom curve",
            ["要最小化到托盘继续运行，还是直接退出？"] = "Minimize to tray and keep running, or exit now?",
            ["记住我的选择（可在「配置 → 关闭行为」中修改）"] = "Remember my choice (change it in Profiles → Close button behavior)",
            ["设置封面…"] = "Set cover…",
            ["该文件夹内没有找到 exe"] = "No .exe found in that folder",
            ["请先选择一个配置"] = "Select a profile first",
            ["读取失败（ADLX 未返回状态）"] = "Read failed (ADLX returned no state)",
            ["读取当前时序失败"] = "Failed to read current timings",
            ["超分"] = "Upscaling",
            ["超频预设已达上限 5 个，请先删除或覆盖已有预设"] = "OC preset limit (5) reached; delete or overwrite an existing preset",
            ["进程配置应用失败：{r.Detail}"] = "Failed to apply process profile: {r.Detail}",
            ["选择游戏封面"] = "Choose game cover",
            ["选择游戏文件夹"] = "Choose game folder",
            ["配置不存在"] = "Profile does not exist",
            ["频率偏移允许"] = "Clock offset allowed",
            ["频率预测区间（按负载类型估算，非驱动回读）"] = "Predicted clock range (estimated by load type, not read from the driver)",
            ["驱动限额（OD8 escape 只读）"] = "Driver limits (OD8 escape, read-only)",
            ["＋ 新建预设"] = "＋ New preset",
            [")}（回读一致）"] = ") (read-back matches)",
            [")}（已保存，进入该游戏时生效）"] = ") (saved; applies when this game starts)",
            ["+ 添加游戏"] = "+ Add game",
            ["10-bit 需要显示器与线材支持；切换后立即生效，黑屏会自动回滚。"] = "10-bit requires display and cable support; applies immediately and rolls back if the screen goes black.",
            ["AMD FSR 帧生成：在真实帧之间插入生成帧以提高显示帧率，需要游戏支持，可能增加输入延迟。"] = "AMD FSR frame gen: inserts generated frames between real ones to raise output frame rate; needs game support and may add input latency.",
            ["AMD FSR 超分辨率：驱动级覆盖项。开启后由驱动覆盖游戏内的 FSR 设置；游戏自身不支持时无效。"] = "AMD FSR upscaling: driver-level override; when on, the driver overrides in-game FSR settings and does nothing if the game lacks support.",
            ["AMD Fluid Motion Frames：驱动级帧生成，不依赖游戏支持；快速移动场景可能出现轻微抖动。"] = "AMD Fluid Motion Frames: driver-level frame generation independent of game support; fast motion may show slight judder.",
            ["AMD 的 VRR 有多个商品名，驱动报告的是同一能力位。"] = "AMD markets VRR under several names; the driver reports the same capability bit.",
            ["Frame Rate Target Control：为全局或单个游戏设定帧率上限，降低功耗与发热。"] = "Frame Rate Target Control: cap frame rate globally or per game to cut power and heat.",
            ["FreeSync / VRR：让显示器刷新率跟随游戏帧率，消除撕裂与卡顿；需显示器与连接方式支持。"] = "FreeSync / VRR: match refresh rate to frame rate to remove tearing and stutter; requires display and connection support.",
            ["FreeSync · FreeSync Premium · FreeSync Premium Pro · Adaptive-Sync（同一能力）"] = "FreeSync · FreeSync Premium · FreeSync Premium Pro · Adaptive-Sync (same capability)",
            ["Image Sharpening 2：锐化画面细节，可在驱动层对所有游戏生效；过度锐化会产生白边。"] = "Image Sharpening 2: sharpens detail driver-wide for all games; over-sharpening causes halos.",
            ["RGB 4:4:4 为完整色度信号；YCbCr 常见于 HDMI 连接，部分格式显示器不支持。"] = "RGB 4:4:4 carries full chroma; YCbCr is common over HDMI and some formats are unsupported by the display.",
            ["RSR 锐化强度：0–100%，按 10 递增；由 ADL RSR Sharpening Strength 控制。"] = "RSR sharpness: 0–100% in steps of 10, driven by ADL RSR Sharpening Strength.",
            ["Radeon Anti-Lag：降低 CPU 到 GPU 的排队延迟，适合竞技射击类游戏；开启后可能轻微降低平均帧率。"] = "Radeon Anti-Lag: reduces CPU-to-GPU queue latency for competitive shooters; may slightly lower average frame rate.",
            ["Radeon Boost：快速移动视角时临时降低分辨率以提升帧率，静止时恢复原生分辨率。"] = "Radeon Boost: temporarily lowers resolution during fast camera motion to raise frame rate, restoring native resolution at rest.",
            ["Radeon Chill：根据画面动静动态调节帧率上限以降低功耗与温度，竞技场景下不建议开启。"] = "Radeon Chill: dynamically caps frame rate based on motion to cut power and heat; not recommended for competitive play.",
            ["Radeon Enhanced Sync：替代传统垂直同步，减少撕裂同时避免帧率被锁死；帧率高于刷新率时有效。"] = "Radeon Enhanced Sync: replaces classic V-Sync, reducing tearing without locking frame rate; effective above refresh rate.",
            ["Radeon Super Resolution：游戏内先用较低分辨率渲染，再由驱动放大到显示器分辨率，几乎所有游戏可用。"] = "Radeon Super Resolution: games render below native and the driver upscales to display resolution; works in almost any game.",
            ["{crCount} 项（点击新建）"] = "{crCount} entries (click New)",
            ["{key} 写入失败：{r.Detail}"] = "Failed to write {key}: {r.Detail}",
            ["{kv.Value.Toggles.Count} 项开关"] = "{kv.Value.Toggles.Count} switches",
            ["{name} 已被隔离，需手动解除"] = "{name} has been isolated and must be released manually",
            ["位深已切换：{back.ToString().Split('"] = "')[0]}|Color depth switched: {back.ToString().Split('|')[0]}",
            ["作用域已切到：{captured.Name}（修改只影响该游戏）"] = "Scope switched to: {captured.Name} (changes affect this game only)",
            ["切换失败（rc={rc}），已恢复 {bw}×{bh} @ {bhz} Hz"] = "Switch failed (rc={rc}); restored {bw}×{bh} @ {bhz} Hz",
            ["功耗 {kv.Value.PowerPct:+0;-0;0}%"] = "Power {kv.Value.PowerPct:+0;-0;0}%",
            ["已切换到 {a[0]}×{a[1]} @ {a[2]} Hz 并保留"] = "Switched to {a[0]}×{a[1]} @ {a[2]} Hz and kept it",
            ["已创建 {dlg.Width2}×{dlg.Height2} @ {dlg.Refresh} Hz（可在分辨率下拉中选择）"] = "Created {dlg.Width2}×{dlg.Height2} @ {dlg.Refresh} Hz (available in the resolution list)",
            ["已加载预设 {pr.Name} 到滑杆（点 APPLY 生效）"] = "Loaded preset {pr.Name} into the sliders (click APPLY to apply)",
            ["已重置 {keys.Count} 个游戏配置（全部继承全局）"] = "Reset {keys.Count} game profiles (all now inherit Global)",
            ["最低声学限制：{(int)ev.NewValue} MHz（{lo}–{hi}）"] = "Min acoustic limit: {(int)ev.NewValue} MHz ({lo}–{hi})",
            ["最低风扇转速：{(int)ev.NewValue} %（{lo}–{hi}）"] = "Min fan speed: {(int)ev.NewValue} % ({lo}–{hi})",
            ["未检测到 VRR 能力（FreeSync / Premium / Premium Pro / Adaptive-Sync 均未报告）"] = "No VRR capability detected (FreeSync / Premium / Premium Pro / Adaptive-Sync not reported)",
            ["检测到上次显示模式未确认，已恢复 {w}×{hh} @ {hz} Hz"] = "Previous display mode was not confirmed; restored {w}×{hh} @ {hz} Hz",
            ["点「扫描游戏」自动识别 Steam / Epic，或「添加文件夹」手动指定"] = "Click Scan games to detect Steam / Epic, or Add folder to pick manually",
            ["粘贴 SteamGridDB API Key（免费，在 steamgriddb.com/profile/preferences/api 生成）："] = "Paste a SteamGridDB API key (free; create one at steamgriddb.com/profile/preferences/api):",
            ["缩放模式 → {scNames[Math.Clamp(int.Parse(b2[1]), 0, 2)]}（回读一致）"] = "Scaling mode → {scNames[Math.Clamp(int.Parse(b2[1]), 0, 2)]} (read-back matches)",
            ["获取失败：请检查 API Key 是否有效，或该游戏在 SteamGridDB 无条目"] = "Fetch failed: check the API key, or the game may not exist on SteamGridDB",
            ["随当前分辨率变化。高刷新率需要 DisplayPort 线材与显示器 OSD 同时开启。"] = "Follows the current resolution. High refresh rates need a DisplayPort cable and the monitor OSD enabled.",
            ["非原生分辨率时如何铺满屏幕：保持纵横比、全面板拉伸或居中不缩放。"] = "How non-native resolutions fill the screen: preserve aspect, stretch to panel, or centre without scaling.",
            ["频率 {kv.Value.FreqOffset:+#;-#;0} MHz"] = "Clock {kv.Value.FreqOffset:+#;-#;0} MHz",
            ["驱动未暴露营销等级，这里按 EDID 的刷新率范围与 HDR 元数据推断。"] = "The driver does not expose a marketing tier; inferred from EDID refresh range and HDR metadata.",
            ["覆盖"] = "Override",
            ["继承全局"] = "Inherit global",
            ["全局=开"] = "global=on",
            ["全局=关"] = "global=off",
            ["FSR 超分"] = "FSR upscaling",
            ["FSR 帧生成"] = "FSR frame gen",
            ["游戏自带 FSR 超分运行时"] = "Game FSR upscaling runtime",
            ["游戏自带帧生成运行时"] = "Game frame-gen runtime",
            ["驱动级覆盖"] = "Driver-level override",
            ["开启后会覆盖游戏内设置"] = "when on, overrides in-game settings",
            ["GPU 缩放"] = "GPU scaling",
            ["值"] = "Value",
            ["游戏内设置"] = "in-game settings",
            ["驱动级"] = "Driver-level",
            ["开启后"] = "when enabled",
            ["支持"] = "supports",
            ["范围"] = "range",
            ["电压"] = "Voltage",
            ["功耗"] = "Power",
            ["频率"] = "Clock",
            ["风扇"] = "Fan",
            ["自定义曲线"] = "Custom curve",
        };

        private static readonly (string zh, string en)[] Tokens =
        {
            ("（只读）", " (read-only)"), ("（同一能力）", " (same capability)"),
            ("（EDID 推断）", " (EDID inference)"), ("（点击新建）", " (click New)"),
            ("（VSR）", " (VSR)"),
            ("范围 ", "range "), ("支持 ", "supports "), ("含 ", "with "),
            ("当前已启用", "currently enabled"), ("当前未启用", "currently disabled"),
            ("元数据", "metadata"), ("（仅修改该配置）", " (changes this profile only)"), ("，全局=", ", Global="), ("本游戏=", "This game="), ("（回读一致）", " (read-back matches)"), ("已切换：", "switched: "), ("，", ", "), ("、", " · "),
        };

        private static string Tokens2(string s)
        {
            foreach (var (zh, en) in Tokens) s = s.Replace(zh, en);
            return s;
        }

        private static string[] _phraseKeys;

        private static string Phrases(string s)
        {
            if (_phraseKeys == null)
            {
                var ks = new List<string>();
                foreach (var k in En.Keys) if (k.Length >= 2) ks.Add(k);
                ks.Sort((a, b) => b.Length.CompareTo(a.Length));
                _phraseKeys = ks.ToArray();
            }
            foreach (var k in _phraseKeys)
                if (s.Contains(k, StringComparison.Ordinal)) s = s.Replace(k, En[k]);
            return s;
        }


        private static string[] _revKeys;
        private static Dictionary<string, string> _rev;

        private static void BuildRev()
        {
            _rev = new Dictionary<string, string>();
            foreach (var kv in En) if (!_rev.ContainsKey(kv.Value)) _rev[kv.Value] = kv.Key;
            var ks = new List<string>(_rev.Keys);
            ks.Sort((a, b) => b.Length.CompareTo(a.Length));
            _revKeys = ks.ToArray();
        }

        /// <summary>英文文本还原为中文（切回中文界面时用）。</summary>
        public static string Zh(string en)
        {
            if (string.IsNullOrEmpty(en)) return en;
            if (_rev == null) BuildRev();
            if (_rev.TryGetValue(en, out var zh)) return zh;
            var s2 = en;
            foreach (var k in _revKeys)
                if (s2.Contains(k, StringComparison.Ordinal)) s2 = s2.Replace(k, _rev[k]);
            return s2;
        }

        public static void ApplyZh(DependencyObject root)
        {
            if (root == null) return;
            switch (root)
            {
                case TextBlock tb when !string.IsNullOrEmpty(tb.Text): tb.Text = Zh(tb.Text); break;
                case Button b when b.Content is string bs: b.Content = Zh(bs); break;
                case CheckBox cb when cb.Content is string cs: cb.Content = Zh(cs); break;
                case Window w: w.Title = Zh(w.Title); break;
            }
            if (root is FrameworkElement fe && fe.ToolTip is string tip) fe.ToolTip = Zh(tip);
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++) ApplyZh(VisualTreeHelper.GetChild(root, i));
        }

        public static string T(string zh)
        {
            if (Language != "en" || string.IsNullOrEmpty(zh)) return zh;
            if (En.TryGetValue(zh, out var exact)) return exact;
            // 长串（带括号/单位）用最长前缀匹配，保留尾部
            string best = null; int bestLen = 0;
            foreach (var kv in En)
            {
                if (kv.Key.Length > bestLen && kv.Key.Length >= 4 && zh.StartsWith(kv.Key, StringComparison.Ordinal))
                {
                    best = kv.Value; bestLen = kv.Key.Length;
                }
            }
            if (best == null) return Tokens2(Phrases(zh));   // 无整句词条：按短语替换
            var rest = zh.Substring(bestLen);
            foreach (var ch in rest)
                if (ch > 0x2E80 && ch < 0xA4D0) return Tokens2(Phrases(zh));
            return best + rest;
        }

        /// <summary>遍历可视树翻译静态文字（TextBlock/Button/CheckBox/ToolTip/窗口标题）。</summary>
        public static void Apply(DependencyObject root)
        {
            if (root == null) return;
            switch (root)
            {
                case TextBlock tb when !string.IsNullOrEmpty(tb.Text):
                    tb.Text = T(tb.Text);
                    break;
                case Button b when b.Content is string bs:
                    b.Content = T(bs);
                    break;
                case CheckBox cb when cb.Content is string cs:
                    cb.Content = T(cs);
                    break;
                case Window w:
                    w.Title = T(w.Title);
                    break;
            }
            if (root is FrameworkElement fe && fe.ToolTip is string tip) fe.ToolTip = T(tip);
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++) Apply(VisualTreeHelper.GetChild(root, i));
        }
    }
}
