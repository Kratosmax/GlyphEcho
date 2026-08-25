# GlyphEcho

GlyphEcho 是一个 Windows 10/11 桌面输入回显工具：在后台监听键盘、XInput 手柄和通用 HID 手柄，在不抢焦点的叠加层中显示当前输入，并按前台应用规则决定展示范围、级别和样式。

## 使用者教程

### 运行要求

- Windows 10/11 x64
- .NET 8 Desktop Runtime（Lite 包）
- 全局键盘监听需要运行在真实 Windows 桌面会话中

### 运行方式

Release 提供四种包：Full/Lite Setup 安装器，以及 Full/Lite ZIP 便携版。Full 包自包含 .NET 运行时，Lite 包需要 .NET 8 Desktop Runtime。程序启动后常驻系统托盘；关闭主窗口默认缩小到托盘，可在设置中改为退出程序。

主窗口提供三个页面：概览、应用规则、按键目录。按键目录会自动记录监听到的组合键，可以搜索、启用/禁用和删除；应用规则可以引用全局目录并覆盖指定应用的差异项。

“网络与更新”页提供 Lite/Full 通道选择、GitHub 更新源连通性测试、签名清单检查和安全更新入口。纯 Shift 文本输入（例如 `Shift + 1`、`Shift + /`）不会自动写入快捷键目录。

### 手柄

标准 XInput 手柄支持 A/B/X/Y、方向键、肩键、扳机、摇杆和 Start/Back。通用 HID 扩展按键使用 `M1`、`M2`、`M3`、`M4` 缩写。飞智黑武士 4 Pro 的专属 HID 报告映射需要连接真实设备后确认，当前不会假定固定 VID/PID 或报告布局。

### 配置与故障排查

默认配置位置为 `%LOCALAPPDATA%\GlyphEcho\settings.json`。测试或隔离运行时可设置 `KEYOVERLAY_DATA_DIR` 环境变量；该变量名称为旧版本兼容入口。配置损坏时程序会先备份原文件并显示警告，不会静默覆盖用户数据。

如果界面显示“监听失败”，请确认程序运行在 Windows 桌面会话，并检查是否有安全软件拦截低级键盘钩子。程序不会因为 XInput/HID 手柄不可用而阻止键盘功能。

## 自行编译

要求 .NET SDK 8.0 或更高版本，以及 Windows Desktop SDK。

```powershell
dotnet restore .\GlyphEcho.csproj
dotnet build .\GlyphEcho.csproj --configuration Release
dotnet run --project .\GlyphEcho.csproj --configuration Release
dotnet publish .\GlyphEcho.csproj --configuration Release --runtime win-x64 --self-contained false --output .\temp\preview\GlyphEcho-0.2.1
```

产物位于 `bin\Release\net8.0-windows`；本地预览包位于项目级 `temp\preview`。项目仅支持 Windows，Linux/macOS 不能替代真实桌面会话验证全局输入监听。

## AI 继续开发

下一次接续开发前先读取根目录的 [`CODEX_PROGRESS.md`](CODEX_PROGRESS.md)，再运行其中列出的校准命令。关键入口如下：

- `App.xaml.cs`：设置加载、单实例、托盘、规则合并和生命周期
- `KeyboardHook.cs`：全局键盘钩子、按键规范化和前台进程识别
- `GamepadHook.cs`：XInput 轮询
- `HidGamepadHook.cs`：通用 Raw Input/HID 扩展按键入口
- `MainWindow.xaml(.cs)`：设置、规则和按键目录 UI
- `OverlayWindow.xaml(.cs)`：叠加层展示
- `GlyphEcho.csproj`：唯一版本源（当前 `0.2.1`）
- `GlyphEcho.Updater/`：等待退出、校验、暂存、替换和回滚
- `.github/workflows/release.yml`：标签触发的四资产发布流水线

必须保留：低级简约模式使用独立按键按钮、默认规则与应用规则的继承关系、配置原子写入、单实例限制、损坏配置备份、HID 扩展键使用通用 `M1-M4` 命名。禁止提交 `bin/`、`obj/`、`temp/`、用户配置、日志、令牌或私钥。

提交、推送、创建 Release 和覆盖线上资产是分开的授权动作；没有用户明确授权时只能停在本地提交。UI 或功能改动必须在真实 Windows 会话启动验证，并留下可重跑命令和产物路径。
