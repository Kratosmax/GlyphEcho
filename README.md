# GlyphEcho

GlyphEcho 是一个 Windows 10/11 桌面输入回显工具：在后台监听键盘和 XInput 手柄，在不抢焦点的叠加层中显示当前输入，并按前台应用规则决定展示范围、级别和样式。

## 使用者教程

### 运行要求

- Windows 10/11 x64
- .NET 8 Desktop Runtime（Lite 包）
- 全局键盘监听需要运行在真实 Windows 桌面会话中

### 运行方式

Release 提供四种包：Full/Lite Setup 安装器，以及 Full/Lite ZIP 便携版。Full 包自包含 .NET 运行时，Lite 包需要 .NET 8 Desktop Runtime。程序启动后常驻系统托盘；关闭主窗口默认缩小到托盘，可在设置中改为退出程序。当前用户开机自启默认开启，可在设置页关闭；自启使用后台模式，不会主动弹出主窗口。

> **0.3.1 / 0.4.0 非系统盘便携版升级提示：**旧更新器无法跨磁盘移动事务目录。请先从 [Releases](https://github.com/Kratosmax/GlyphEcho/releases) 下载同通道的 0.6.0 ZIP，右键托盘图标退出 GlyphEcho，再把 ZIP 解压覆盖原程序目录；也可以直接运行 0.6.0 安装器。用户设置保存在 `%LOCALAPPDATA%\GlyphEcho`，手动覆盖程序文件不会删除设置。0.5.0 起已修复后续跨卷自动更新。

主窗口提供五个页面：概览、应用规则、按键目录、网络与更新、设置。按键目录会自动记录监听到的组合键，可以搜索、启用/禁用和删除；应用规则可以引用全局目录并覆盖指定应用的差异项；设置页集中管理开机自启、关闭窗口行为和 Windows 11 毛玻璃。

模式切换会立即保存并作用于键盘、手柄和预览：普通模式遵循默认规则与应用规则；游戏模式强制显示单键，并允许选择低级或中级提示；演示模式强制显示单键并展示按键来源与功能说明。模式下拉框和各选项提供悬浮说明，被模式覆盖的设置会暂时禁用，切回普通模式后恢复原配置。

四个展示位置分别保存 X/Y 像素微调，X 正数向右、Y 正数向下。例如把“右下”的 X 设为 `-10`，以后在任意显示器选择“右下”都会向左偏移 10 个物理像素；“重置当前位置”只清零当前锚点。为避免提示完全移出屏幕，最终坐标会限制在目标显示器工作区内。

概览页提供六个提示色板：深色的薄荷绿、天空蓝、琥珀黄，以及浅色的深青、钴蓝、玫红。色板用 `×1 / ×2 / ×3` 直接预览重复次数文字颜色，选择后立即保存并应用到提示层。

“网络与更新”页提供 Lite/Full 通道选择、GitHub 更新源连通性测试、签名清单检查和安全更新入口。纯 Shift 文本输入（例如 `Shift + 1`、`Shift + /`）不会自动写入快捷键目录。

GitHub 前缀线路和 HTTP 网络代理是两层独立设置：前缀线路会把完整 GitHub 下载 URL 交给所选第三方服务；HTTP 代理只负责底层出网。无论使用哪条线路，程序都会执行相同的清单签名、包大小、SHA-256、通道和 ZIP 结构校验。

### 手柄

标准 XInput 手柄支持 A/B/X/Y、方向键、肩键、扳机、左右摇杆八方向和 Start/Back。摇杆使用进入/退出双阈值死区，方向事件最短间隔为 200ms。飞智黑武士 4 Pro 在当前系统只暴露标准 XInput 接口，M1-M4 没有可验证的独立字段；请在飞智空间站把背键映射为 F13-F16，GlyphEcho 会依次显示为 M1-M4。程序不再猜测未知 Raw HID 字节，避免把右摇杆误报为 M 键。

提示层每 200ms 合并一次输入：相同的可见提示显示 `× N`，不同提示按队列展示并在到期后依次前移。低级提示只按可见按键合并，不会因为后台采样到不同应用而生成两条相同内容；中高级仍按可见来源和功能区分。

### 配置与故障排查

默认配置位置为 `%LOCALAPPDATA%\GlyphEcho\settings.json`。测试或隔离运行时可设置 `KEYOVERLAY_DATA_DIR` 环境变量；该变量名称为旧版本兼容入口。配置损坏时程序会先备份原文件并显示警告，不会静默覆盖用户数据。

如果界面显示“监听失败”，请确认程序运行在 Windows 桌面会话，并检查是否有安全软件拦截低级键盘钩子。程序不会因为 XInput 手柄不可用而阻止键盘功能。

## 自行编译

要求 .NET SDK 8.0 或更高版本，以及 Windows Desktop SDK。

```powershell
dotnet restore .\GlyphEcho.csproj
dotnet build .\GlyphEcho.csproj --configuration Release
dotnet run --project .\GlyphEcho.csproj --configuration Release
dotnet run --project .\GlyphEcho.SmokeTests\GlyphEcho.SmokeTests.csproj --configuration Release
dotnet run --project .\GlyphEcho.Updater\GlyphEcho.Updater.csproj --configuration Release -- --self-test
.\scripts\Build-Preview.ps1
```

产物位于 `bin\Release\net8.0-windows`；`Build-Preview.ps1` 会在项目级 `temp\preview` 生成标记为本地验收用途的 Lite 目录和 ZIP。预览名称包含 ICO 内容哈希，Logo 改变后会使用新路径，避免 Windows Explorer 复用旧图标缓存。项目仅支持 Windows，Linux/macOS 不能替代真实桌面会话验证全局输入监听。

无需控制鼠标键盘即可生成真实 WPF 窗口截图：

```powershell
$env:KEYOVERLAY_DATA_DIR = ".\temp\visual-qa-data"
.\bin\Release\net8.0-windows\GlyphEcho.exe --capture-ui ".\temp\ui-captures"
```

## AI 继续开发

下一次接续开发前先读取根目录的 [`CODEX_PROGRESS.md`](CODEX_PROGRESS.md)，再运行其中列出的校准命令。关键入口如下：

- `App.xaml.cs`：设置加载、单实例、托盘、规则合并和生命周期
- `StartupRegistration.cs`：当前用户开机自启的查询、修复、启用和禁用
- `KeyboardHook.cs`：全局键盘钩子、按键规范化和前台进程识别
- `GamepadHook.cs`：XInput 轮询
- `MainWindow.xaml(.cs)`：设置、规则和按键目录 UI
- `OverlayQueue.cs`、`OverlayWindow.Queue.cs`：200ms 动态提示队列和叠加层展示
- `GlyphEcho.csproj`：唯一版本源（当前 `0.6.0`）
- `GlyphEcho.Updater/`：等待退出、校验、暂存、替换和回滚
- `GlyphEcho.SmokeTests/`：摇杆、队列、网络、签名和更新包结构回归
- `VisualQaRunner.cs`：隔离、无输入控制的后台 UI 截图验收
- `.github/workflows/release.yml`：标签触发的四资产发布流水线

必须保留：低级简约模式使用独立按键按钮、默认规则与应用规则的继承关系、配置原子写入、单实例限制、损坏配置备份，以及 F13-F16 到 M1-M4 的兼容映射。没有描述符或厂商协议证据时禁止恢复 Raw HID 位猜测。禁止提交 `bin/`、`obj/`、`temp/`、用户配置、日志、令牌或私钥。

提交、推送、创建 Release 和覆盖线上资产是分开的授权动作；没有用户明确授权时只能停在本地提交。UI 或功能改动必须在真实 Windows 会话启动验证，并留下可重跑命令和产物路径。
