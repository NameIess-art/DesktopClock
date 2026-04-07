# DesktopClock

一个基于 `WinForms + .NET 8` 的极简桌面时钟。

它默认显示在桌面层，尽量不打扰正常办公窗口；主要入口只有时钟本体和系统托盘菜单，适合作为轻量、常驻、低占用的桌面时间显示工具。

## 特性

- 桌面层显示，普通情况下不覆盖常规应用窗口
- 编辑模式与展示模式分离
- 支持时间、日期、星期拆分显示，并允许时间使用自定义 .NET 格式
- 按显示格式切换刷新策略，避免无效刷新
- 支持背景颜色、透明度、尺寸、圆角调整
- 支持字体切换、字体搜索、打开系统字体文件夹
- 支持纯色文字和双颜色渐变文字
- 支持左右 / 上下渐变方向
- 支持当前用户开机自启
- 托盘图标根据系统深浅色主题切换黑白样式
- 配置损坏时自动回退默认值，不阻止应用启动

## 当前实现

- 技术栈：`C# / .NET 8 / Windows Forms`
- 绘制方式：主时钟窗口使用 layered window 进行逐像素 alpha 合成
- 配置存储：本地 `settings.json`
- 托盘：`System.Windows.Forms.NotifyIcon`
- 自启：当前用户注册表 `Run` 项

## 使用方式

启动应用后：

- 桌面上会显示时钟
- 任务栏不会出现普通窗口入口
- 系统托盘会出现时钟图标

托盘菜单支持：

- `编辑模式`
- `显示格式`
- `设置...`
- `开机自启`
- `退出`

### 编辑模式

开启编辑模式后：

- 可以拖动时钟位置
- 可以使用滚轮缩放
- 可以通过四角手柄缩放

关闭编辑模式后：

- 窗口变为不可交互展示层
- 位置和缩放会被保留

### 设置页

设置页整合为单个窗口，顶部导航可切换：

- `背景`
- `颜色`
- `字体`

其中字体页支持：

- 搜索系统字体
- 选择字体族
- 打开系统字体文件夹

## 显示格式与刷新策略

- 不含秒的时间格式：按下一分钟边界刷新
- 含秒的时间格式：按下一秒边界刷新

界面只在文本实际变化时更新，尽量减少无意义重绘。

## 配置文件位置

配置默认保存在：

```text
%LOCALAPPDATA%\DesktopClock\settings.json
```

通常对应：

```text
C:\Users\<用户名>\AppData\Local\DesktopClock\settings.json
```

## 运行要求

当前仓库支持发布为 `win-x64` 自包含单文件版本，目标机器无需额外安装 `.NET 8 Windows Desktop Runtime` 也可以直接运行。

## 开发运行

在项目目录执行：

```powershell
dotnet build
dotnet run
```

## 打包示例

### 框架依赖单文件

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true
```

发布产物通常位于：

```text
bin\Release\net8.0-windows\win-x64\publish\
```

## 项目结构

```text
DesktopClock
├─ Dialogs
├─ Forms
├─ Helpers
├─ Models
├─ Native
├─ Services
├─ DesktopClockApplicationContext.cs
├─ Program.cs
└─ DesktopClock.csproj
```

## 已知说明

- 项目目前仅支持单个时钟实例
- 默认使用系统本地时间，不提供时区切换
- 主要面向 Windows 桌面环境
- 桌面层挂载依赖系统桌面窗口结构，不同系统环境下可能存在细微行为差异

## License

当前仓库暂未附带独立许可证文件；如需开源分发，建议补充 `LICENSE`。
