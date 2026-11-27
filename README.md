# 寸度 · Slant

Windows 10 / 11 原生实时光影动态壁纸与系统锁屏应用（基于 WebView2 + WebGL）。

![寸度 · Slant](assets/poster.jpg)

## 核心特性

- **全天候光影节律**：按真实本地时间或加速周期模拟窗影移动与色温演变，包含三段式自然日出（破晓冷墙微晕 → 朝阳入室 → 暖金晨光）、午后、黄昏余晖与深夜月影。
- **实况天气与非匀速呼吸风**：接入 Open-Meteo 实时天气与阵风数据，支持晴、雨、雷暴（带闪电）、落雪动态渲染；风速强弱起伏与阵风包络实时驱动树影摇曳、雨丝偏斜、玻璃水滴滑落与雪花飘移。
- **四季景物 × 10 款窗框**：支持春樱、夏叶、秋枫、冬枝切换，内置极简十字窗（默认）、百叶窗、中式花窗、日式障子、法式拱窗、哥特尖拱、摩洛哥拱窗、威尼斯三联拱、田字窗、无窗格共 10 种窗影光学模型。
- **原生桌面壁纸与 WinRT 锁屏**：支持将实时 WebGL 画面嵌入桌面图标底层或输出物理分辨率静态壁纸，无需管理员权限即可设置 Windows 锁屏并支持定时/解锁自动同步。
- **轻量节电**：检测到全屏游戏或全屏视频时自动暂停动态渲染；控制面板最小化或隐藏至托盘后自动挂起。
- **多工具兼容**：同时提供 `project.json` 与 `LivelyInfo.json`，可直接导入 Wallpaper Engine 或 Lively Wallpaper。

---

## English Overview

**Slant (寸度)** is a lightweight native Windows 10/11 live desktop wallpaper and lock screen app powered by WebView2 and custom WebGL optics shaders. It projects realistic circadian window sunlight/moonlight across textured interior walls, reacts in real time to local weather and stochastic gusts (rain, thunderstorms with lightning, soft bokeh snow, frost), and includes 4 seasonal foliage modes plus 10 architectural mullion frames (Cross window by default, Venetian blinds, Chinese lattice, Japanese shoji, French/Gothic/Moorish/Venetian arches, etc.).

## 运行要求与从源码编译

- **运行环境**：Windows 10 (`19041+`) / Windows 11，.NET 8 Desktop Runtime (`x64`)，Microsoft Edge WebView2 Runtime
- **编译脚本**：安装 .NET 8 SDK 后，直接运行根目录的 [Build-EXE.bat](Build-EXE.bat)，或执行：

```powershell
dotnet publish src\ObliqueSeasons.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish_out
```

- **命令行参数**：
  - `--window`：以独立预览窗口启动（不自动挂载桌面壁纸）
  - `--desktop` / `--minimized` / `--autostart`：静默启动至系统托盘并恢复动态壁纸

## 隐私说明

仅在启用「实况天气」时请求公共 IP 定位与 Open-Meteo 天气接口；程序不包含任何遥测、统计上报或账号上传逻辑，所有配置均保存在本地。

## 开源协议

本项目基于 [GNU General Public License v3.0 (GPL-3.0)](LICENSE) 开源。
