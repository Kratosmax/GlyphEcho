# GlyphEcho

GlyphEcho 是一个 Windows 10/11 桌面输入回显工具。它在后台监听全局键盘、XInput 和通用 HID 手柄事件，在屏幕右下角以不抢焦点的叠加层展示输入，并按前台进程应用规则。

## 当前 MVP

- 默认规则与按进程覆盖规则，覆盖项缺失时继承默认规则。
- 低/中/高三级展示：按键、组合键、来源与功能说明。
- 可配置忽略按键，设置保存到 `%LOCALAPPDATA%\GlyphEcho\settings.json`。
- 托盘常驻，设置窗口和叠加层独立存在。
- XInput 标准手柄按键和通用 HID 扩展按键预制支持；扩展按键使用 `M1`、`M2`、`M3`、`M4` 缩写。

飞智黑武士 4 Pro 的厂商专属 HID 报告映射需要连接真实设备后确认，当前不会假定固定 VID/PID 或报告布局。

## 本地运行

```powershell
dotnet restore .\GlyphEcho\GlyphEcho.csproj
dotnet run --project .\GlyphEcho\GlyphEcho.csproj --configuration Release
```

本项目仅支持 Windows；低级键盘 hook 需要在真实桌面会话中验证，不能在无窗口的 CI 中代替验证。
