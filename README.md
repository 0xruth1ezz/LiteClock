# 轻时钟 LiteClock

可自定义的 Windows 桌面时钟，使用 C# / WPF 实现，可独立运行，无需安装 ElevenClock、Python 或 Qt。

## 功能

- 自定义宽高、屏幕位置、对齐、内边距、行高和间距，支持拖动调宽及移动。
- 最多 8 行日期、时间和固定文字，支持时区、语言及时间偏移。
- 默认样式与逐行字体、字号、颜色、偏移和显示开关。
- 背景、透明度、圆角、边框及鼠标悬停颜色。
- 设置实时预览，右上角同步显示实际时钟画面，并标注缩放比例。
- 数字输入支持增减按钮、长按、方向键、聚焦后的滚轮以及范围校验。
- 托盘菜单、点击动作、鼠标穿透、全屏隐藏及可选的开机启动。
- JSON 配置导入、导出及备份恢复。

## 构建与运行

需要 Windows 10/11 和 .NET Framework 4.8。在项目目录中打开 PowerShell：

```powershell
.\build.ps1
.\LiteClock.exe
```

构建脚本使用 Windows 自带的 .NET Framework C# 编译器。图标和应用清单包含在源码中，无需下载依赖。

首次运行使用内置默认样式。个人设置保存到程序旁的 `settings.json`，不纳入版本控制。再次运行程序会打开设置窗口。

```powershell
.\LiteClock.exe --settings
.\LiteClock.exe --exit
```

重新构建前，请退出从本项目目录启动的时钟。

## 测试

先执行构建，再运行：

```powershell
Start-Process -FilePath .\LiteClock.exe -ArgumentList '--self-test', 'tests\self-test-results.txt' -Wait
Get-Content .\tests\self-test-results.txt
.\tests\run-number-tests.ps1
```

自检覆盖日期边界、时区、配置保存与恢复；数字控件测试覆盖范围、步长、小数精度及编辑状态。测试输出不纳入版本控制。

## 文件

- `LiteClock.cs`：时钟窗口、配置、时间格式、托盘与启动入口。
- `SettingsWindow.cs`：设置窗口与实时预览。
- `NumberInput.cs`：数字输入控件。
- `LiteClock.png`、`LiteClock.ico`：高清图标与 Windows 多尺寸图标；`build-icon.ps1` 用于重新生成 ICO。
- [使用说明](使用说明.txt)：完整操作说明与当前限制。
- [图标设计说明](图标设计说明.txt)：图标生成记录。

本项目独立于 ElevenClock，使用 Windows 系统时间，不提供独立的网络对时服务。
