# 轻时钟 LiteClock

可自定义的 Windows 桌面时钟，使用 C# / .NET 8 / WinUI 3（Windows App SDK 1.8）实现。所有应用界面都使用 WinUI 3，不依赖 WPF 或 Windows Forms。

## 下载

从 [GitHub Releases](https://github.com/0xruth1ezz/LiteClock/releases) 下载 Windows x64 压缩包，完整解压后运行其中的 `LiteClock.exe`。支持 Windows 10 2004 或更新版本；保留同目录的运行时文件，无需另装 .NET。发布包不包含个人配置，首次运行使用默认样式。

## 功能

- 自定义宽高、屏幕位置、对齐、内边距、行高和间距；拖动左边缘调宽，Shift + 拖动移动。
- 最多 8 行日期、时间和固定文字，支持时区、语言、时间偏移及逐行样式。
- 背景、文字及整体透明度，圆角、边框和悬停颜色。
- 六页 NavigationView 设置界面、NumberBox 数字输入、ToggleSwitch 开关、ComboBox 字体与选项、ColorPicker 颜色选择、InfoBar 状态反馈。
- 标题栏显示应用版本；“应用信息”页显示版本、运行环境、系统与文件位置，支持复制信息。
- 所有颜色选择器支持“屏幕取色”：像素放大预览、十六进制颜色提示，左键确认，右键或 Esc 取消；可跨显示器采样。
- 桌面时钟与实时预览共用 `ClockView`，同步内容、逐行样式、裁切及缩放比例。
- WinUI MenuFlyout 右键及托盘菜单、CalendarView 日历、ContentDialog 错误提示，以及 Windows 系统文件选择器。
- 置顶、全屏隐藏、鼠标穿透、点击动作及可选的开机启动。
- 兼容旧版 JSON 配置，支持导入、导出、原子保存和备份恢复。

在“设置 → 操作”页顶部打开“随系统启动”，再点击“保存”，即可在当前用户登录 Windows 时自动显示时钟。关闭开关并保存可取消自启动；取消或关闭设置不会更改启动项。首次使用默认关闭，不需要管理员权限。

系统托盘注册、显示器枚举和窗口样式通过 Win32 互操作实现；托盘菜单内容仍使用 WinUI 控件。启动前的致命错误使用系统消息框。

屏幕取色会暂时隐藏设置窗口，从当前桌面快照采样并回填 `#RRGGBB`。快照仅保存在内存中，结束后释放，不生成截图文件。取色结果仍属于设置草稿，点击“保存”后才写入配置。HDR 和受保护的视频内容的采样可能与原始内容的颜色不同。

## 构建与运行

构建需要 Windows 10 2004（19041）或更新版本、.NET 8 SDK（或更新版本）以及首次还原 NuGet 依赖所需的网络连接。无需 Visual Studio；SDK、WinUI 和构建工具版本由工程固定。

```powershell
.\build.ps1
.\dist\LiteClock-x64\LiteClock.exe
```

默认发布 x64 非 MSIX、自包含版本，将 .NET 和 Windows App SDK 运行时一起放在 `dist\LiteClock-x64`。分发时复制整个目录，不能只复制 EXE。运行用户不需要单独安装 .NET 或 Windows App Runtime。部署配置参考 [Windows App SDK 自包含部署文档](https://learn.microsoft.com/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps)。

```powershell
.\build.ps1 -NoPublish             # 仅编译
.\build.ps1 -Platform ARM64        # ARM64 发布，需在 ARM64 Windows 上运行验证
.\dist\LiteClock-x64\LiteClock.exe --settings
.\dist\LiteClock-x64\LiteClock.exe --exit
```

构建脚本可使用 PATH 中的 `dotnet`，也支持项目本地 `.tools\dotnet\dotnet.exe`。重新发布前先退出该发布目录中的时钟。

应用版本统一在 `LiteClock.csproj` 的 `Version` 中维护，标题栏和“应用信息”页读取构建后的程序集版本。

首次发布会将仓库目录已有的 `settings.json` 复制到发布目录；再次发布不会覆盖发布目录的个人配置。通常配置保存在 EXE 旁边，再次运行打开同一个实例的设置。迁移时先退出旧版，再启动新版；原有开机启动路径需在新版设置中重新启用。

也可用 `--settings-dir <目录>` 指定配置位置；同一配置目录共用实例与控制信号，不同目录彼此独立。

## 测试

```powershell
.\build.ps1
.\tests\run-tests.ps1
```

测试包括日期边界、时区／夏令时、JSON 兼容和恢复、全部数字规则、实际加载的 NumberBox 模板与原生增减按钮、输入中的无效草稿、小数精度、WinUI 多行换行符、六个设置页面及取消恢复、日历窗口生命周期，以及真实屏幕像素采样、取色取消恢复、边框开关与置顶切换后的四边像素检查。UI 测试需要已登录的交互式桌面，使用独立配置和实例，不覆盖日常设置。

`tests\run-number-tests.ps1` 可单独执行数字及 WinUI 控件测试；结果写在 `tests\*-results.txt`。混合 DPI、多显示器热插拔、不同 Windows 版本及长时间运行仍需在相应设备验证。

## 源码

- `LiteClock.csproj`、`App.xaml`：WinUI 3 工程与主题资源。
- `LiteClock.cs`：应用生命周期、托盘菜单、日历、单实例与命令行。
- `ClockWindow.cs`、`ClockView.cs`：桌面窗口行为与共享 XAML 渲染组件。
- `SettingsWindow.cs`：原生设置控件、实时预览与导入导出。
- `AppDetails.cs`：统一的版本、窗口标题与应用信息。
- `Settings.cs`、`NumberRules.cs`、`NumberInput.cs`：配置、格式和数字输入。
- `Native.cs`：必要的 Windows 桌面互操作与透明背景。
- `ScreenColorPicker.cs`、`ScreenPixels.cs`：WinUI 屏幕取色覆盖窗口、放大预览和物理像素采样。
- `SelfTests.cs`、`tests`：回归测试及运行脚本。
- `LiteClock.png`、`LiteClock.ico`：应用图标；`build-icon.ps1` 可重新生成 ICO。
- [使用说明](使用说明.txt)：完整操作说明。

本项目独立于 ElevenClock，使用 Windows 系统时间，不提供独立的网络对时服务。
