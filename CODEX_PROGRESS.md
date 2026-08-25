# GlyphEcho 接续记录

## 接手校准

```powershell
git status --short --branch
git branch --show-current
git remote -v
git log -1 --oneline
Get-Content .\GlyphEcho.csproj
```

版本唯一来源是 `GlyphEcho.csproj` 的 `<Version>`，当前为 `0.3.0`。默认分支为 `main`，远程仓库为 `https://github.com/Kratosmax/GlyphEcho.git`。

## 当前快照

- 最后本地核验：2026-08-26
- 提交基线：`e07944c417d29fa258da01a19824ac85e9a3b610`
- 当前状态：0.3.0 候选版已完成本地构建与验证；用户已明确授权提交、推送、创建 `0.3.0` 标签和 GitHub Release，云端流水线尚未运行
- 远程状态：开始工作时本地 `main` 与 `origin/main` 一致；线上 0.3.0 尚未核验
- 当前工作：手柄、提示队列、双屏定位与位置微调、PinNote 风格 UI/毛玻璃、网络更新、模式即时切换、导航对齐、统一 Logo 和预览图标缓存规避已在工作树完成
- 正式候选版本为 `0.3.0`；版本号、主界面显示、更新器发布参数、安装器参数和发布说明已统一

## 本轮实现

- 删除无描述符依据的 Raw HID M1-M4 位猜测，避免右摇杆误报 M 和循环触发
- XInput 支持左右摇杆八方向、进入/退出双阈值死区和 200ms 方向事件间隔
- F13-F16 显示为 M1-M4，作为飞智空间站可验证的背键映射方案
- 提示层每 200ms 合并输入；相同输入显示 `× N`，不同输入排队，1.3 秒到期后前移
- 使用目标显示器 DPI 和物理像素 `SetWindowPos` 定位提示层
- 主窗口、规则编辑器、更新窗和确认框统一 PinNote 浅色语义色、Fluent 图标与 Win11 Acrylic；API 失败或关闭材质时使用不透明回退
- 新增统一应用/托盘/安装器图标及可重跑生成脚本；ICO 改为 16/20/24/32/40/48/64/128/256 九帧标准 32-bit DIB，预览和正式打包前均强制重生成
- 更新网络支持 GitHub URL 前缀线路、独立 HTTP 代理、稳定优先级故障转移和重定向 allowlist
- 自动更新支持旧 `signature` 与新 `signatureV2` 双签名；新签名覆盖产品、通道、版本、URL、大小、哈希和更新说明
- 下载后在主程序退出前检查包入口、更新器、通道、条目数、展开大小、路径越界和符号链接；更新器再次验证并使用 stage/backup/回滚
- “启动后自动检查更新”现在会在启动 5 秒后执行，退出时取消
- 新增隔离的 `--capture-ui` 后台视觉验收和 Lite 本地预览构建脚本
- 普通模式遵循规则；游戏模式强制单键并允许独立保存低/中级；演示模式强制单键和高级来源/功能提示。切换会立即持久化，界面同步显示有效级别并保留各模式原配置
- 修复导航按钮模板忽略 `HorizontalContentAlignment` 导致的菜单起点漂移
- 默认规则滚动内容增加 12px 右侧留白，微调重置按钮在窄窗口改为独立一行，避免与滚动条贴边或被横向截断
- 四个展示位置分别持久化 X/Y 物理像素微调，跨显示器复用同一锚点偏移，支持重置并把最终位置限制在目标工作区
- Logo 改为简约的对话按键小脸；侧栏和全部 WPF 标题栏直接复用 `Assets/GlyphEcho.png`，程序、托盘和安装器 ICO 由同一 PNG 生成
- 本地预览目录和 ZIP 名包含 ICO SHA-256 前 8 位；Logo 内容变化时自动换路径，避免 Explorer 按旧路径复用分尺寸图标缓存
- 主界面从程序集动态显示版本；更新器和安装器不再维护独立默认版本，正式包统一从 `GlyphEcho.csproj` 传入 `0.3.0`

## 修改文件

- UI 与生命周期：`App.xaml`、`App.xaml.cs`、`MainWindow.xaml`、`MainWindow.xaml.cs`、`ModePolicy.cs`、`RuleEditorWindow.xaml`、`RuleEditorWindow.xaml.cs`、`AppDialog.xaml`、`AppDialog.xaml.cs`
- 提示与输入：`GamepadHook.cs`、`KeyboardHook.cs`、`OverlayQueue.cs`、`OverlayWindow.xaml`、`OverlayWindow.Queue.cs`；删除 `HidGamepadHook.cs` 和旧 `OverlayWindow.xaml.cs`
- 原生与视觉验收：`NativeMethods.cs`、`VisualCaptureService.cs`、`VisualQaRunner.cs`
- 更新：`UpdateNetworkSettings.cs`、`UpdateService.cs`、`UpdateWindow.xaml`、`UpdateWindow.xaml.cs`；删除旧 `UpdateManifest.cs`
- 更新器：`GlyphEcho.Updater/GlyphEcho.Updater.csproj`、`GlyphEcho.Updater/UpdaterProgram.cs`；删除旧 `GlyphEcho.Updater/Program.cs`
- 测试与构建：`GlyphEcho.SmokeTests/`、`Properties/AssemblyInfo.cs`、`scripts/Build-Preview.ps1`、`scripts/Generate-AppIcon.ps1`、`scripts/Build-ReleaseAssets.ps1`
- 品牌与发布：`Assets/GlyphEcho.ico`、`Assets/GlyphEcho.png`、`GlyphEcho.csproj`、`installer/GlyphEcho.iss`、`.github/workflows/release.yml`
- 文档：`README.md`、`CODEX_PROGRESS.md`

## 本轮验证

```powershell
dotnet build .\GlyphEcho.SmokeTests\GlyphEcho.SmokeTests.csproj --configuration Release --disable-build-servers
dotnet run --project .\GlyphEcho.SmokeTests\GlyphEcho.SmokeTests.csproj --configuration Release --no-build
dotnet run --project .\GlyphEcho.Updater\GlyphEcho.Updater.csproj --configuration Release --no-restore -- --self-test
.\scripts\Build-Preview.ps1
.\scripts\Build-ReleaseAssets.ps1
git diff --check
git status --short --branch
```

- 主程序与 smoke tests：0 警告、0 错误
- 通过：摇杆方向/死区、提示队列合并/过期、游戏低/中级模式覆盖且不改写原规则、位置微调/边界限制/持久化、网络线路规范化/排序、Legacy/V2 签名与篡改拒绝、更新包结构/通道
- 更新器自测通过：ZIP 路径穿越被拒绝
- 当前设备 XInput 快照：本轮读取到手柄 1，空闲时按钮 `0x0000`、LT/RT 为 0、`LS=(-64,64)`、`RS=(0,0)`；摇杆微小漂移在死区内，未在自动测试期间人工推动摇杆
- 后台 UI 验收：`temp/ui-captures-offset2/ui-capture-status.json` 为 success；普通、游戏低/中、演示、位置微调及其他窗口使用 `PrintWindow` 捕获，控件与滚动条间距、窄窗口布局和统一 Logo 正确，Acrylic 与 frame HRESULT 均为 0，不透明回退通过
- 双屏位置微调：同一“右下 X=-10”在屏幕 1 和屏幕 2 的物理 `Left` 分别为 2372 和 4932，均相对默认右下左移 10px 并落在各自工作区
- Lite 预览真实 EXE 从发布目录启动并完成同一后台 UI 验收；`temp/preview-ui-captures-offset/ui-capture-status.json` 为 success，退出码 0
- 新 Lite EXE 已通过 Windows `PrivateExtractIcons` 从内嵌资源提取 16/32/48/256px 图标，四个尺寸均为新小脸 Logo；证据为 `temp/icon-proof-final-exe-16.png`、`temp/icon-proof-final-exe-32.png`、`temp/icon-proof-final-exe-48.png`、`temp/icon-proof-final-exe-256.png`
- Explorer 图标问题已查实为同版本、同路径下的进程级分尺寸缓存：当前 EXE 只有一个图标组，9 个尺寸均为新 Logo；重启 Explorer 后用户确认恢复正常
- 0.3.0 本地预览包：`temp/preview/GlyphEcho-0.3.0-Lite-local-23028564.zip`，非正式 Release，285,302 字节，SHA-256 `4EE8C2E6FB58B325D84E1291B7475E5546DB630FB44EC1B5066E9366782BBE34`
- 0.3.0 后台 UI 验收：`temp/preview-ui-captures-030/ui-capture-status.json` 为 success；主程序与更新器文件版本均为 `0.3.0.0`，标题栏显示 `GlyphEcho 0.3.0`
- 本地发布 ZIP 展开验证：Lite 285,302 字节、SHA-256 `1DE8273773F993439F359E00BC4794782422505C82E83281A79883F4C2FF34D7`、7 项；Full 99,550,808 字节、SHA-256 `A826FDC7D02D2EBBCC5B279889C3108E717E2DE20CEBDB78F2F69018AB730BFE`、466 项；通道标记和两个 EXE 版本均正确

## 尚未验证

- 未在本轮自动测试窗口内人工推动左右摇杆、按真实 M1-M4；XInput 连接和静止状态已确认
- 飞智黑武士 4 Pro 当前只暴露标准 XInput 字段；原生 M1-M4 仍无可验证协议。需在飞智空间站将背键映射为 F13-F16 后实测
- 未使用上一正式 Release 原始客户端对带真实私钥签名的候选包执行完整升级；正式签名只能在 GitHub Actions 中生成，发布后必须补做线上清单与资产核验
- 本机未安装 Inno Setup，未本地编译安装器；GitHub Actions 会安装 Inno Setup 并构建 Full/Lite Setup，成功前不得宣称 0.3.0 已发布完成

## 关键入口

- `App.xaml.cs`：设置、单实例、托盘、后台更新检查和生命周期
- `GamepadHook.cs` / `KeyboardHook.cs`：XInput 与键盘输入
- `OverlayQueue.cs` / `OverlayWindow.Queue.cs`：动态提示队列
- `NativeMethods.cs`：Acrylic、回退与多屏物理定位
- `UpdateNetworkSettings.cs` / `UpdateService.cs`：网络路由、清单信任、下载和包预检
- `GlyphEcho.Updater/UpdaterProgram.cs`：退出等待、二次验证、暂存替换和回滚
- `VisualQaRunner.cs`：无鼠标键盘控制的真实窗口验收
- `.github/workflows/release.yml`：标签触发的四资产与双签名流水线

## 不得破坏的约束

- 不凭猜测恢复 Raw HID 字节到 M1-M4 的映射
- 保留 0.2.1 客户端需要的 legacy `signature`；新客户端优先验 `signatureV2`
- 所有网络线路执行同一套签名、哈希、通道和包结构校验
- 保留 Per-Monitor-V2 和目标显示器物理像素定位，不能重新用当前窗口的 WPF 变换换算另一块屏幕
- 不提交密钥、令牌、代理凭据、用户配置、`bin/obj/temp`、日志和本地截图
- 用户已授权本次 0.3.0 的提交、推送、标签和 Release；该授权不自动延续到后续版本或覆盖已发布资产

## 维护规则

每次可交付改动后更新日期、提交基线、验证命令、未验证项和授权边界。事实以 Git、源码、构建输出、状态 JSON 和线上 API 为准；未联网核验的远程状态必须明确标记。
