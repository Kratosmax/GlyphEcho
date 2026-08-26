# GlyphEcho 接续记录

## 接手校准

```powershell
git status --short --branch
git branch --show-current
git remote -v
git log -1 --oneline
Get-Content .\GlyphEcho.csproj
```

版本唯一来源是 `GlyphEcho.csproj` 的 `<Version>`，当前为 `0.4.0`。默认分支为 `main`，远程仓库为 `https://github.com/Kratosmax/GlyphEcho.git`。

## 当前快照

- 最后本地核验：2026-08-26
- 提交基线：`ae3e116e0da5096295dca4de89571527909c6d38`（0.4.0 标签）
- 当前状态：0.4.0 已发布；同盘自动升级成功，但非系统盘便携目录存在跨卷替换缺陷
- 远程状态：`main`、`0.4.0` 标签和 Release 已推送；Actions `32923875405` 成功，GitHub `latest` 已指向 0.4.0
- 当前工作：Win10 图标兼容、按键目录批量删除、查重性能优化和当前用户开机自启已随 0.4.0 发布
- 当前正式版本为 `0.4.0`；版本号、主界面显示、更新器发布参数、安装器参数和发布说明已统一

## 当前待办

- P0：更新器 stage 使用系统 `%TEMP%`，当 GlyphEcho 便携目录位于另一卷时，`Directory.Move(stage, target)` 会失败；修复需发布新版本且不能覆盖 0.4.0
- 0.3.1/0.4.0 非系统盘便携用户无法依靠现有自动更新器跨过该缺陷，需要在下一版 README 与 Release Notes 提供手动覆盖安装过渡说明
- 新版本号、修复与再次发布尚未获得用户授权；在获得授权前不创建 0.4.1 标签或 Release

## 本轮实现

- 主窗口、规则编辑器和更新窗的图标字体统一为 Win10 自带的 `Segoe MDL2 Assets`，移除 `Segoe Fluent Icons` 依赖
- 按键目录支持 Ctrl/Shift 多选、全选当前搜索结果、已选计数和带确认的批量删除；选中态横向拉满，单项删除统一居右
- 目录启用开关现在立即同步默认规则并保存，不再依赖其他设置触发持久化
- 按键目录用规范化 `HashSet` 做 O(1) 已存在/已忽略检查；只有新增按键才刷新列表和写配置
- 规则规范化统一加号两侧空格，使 `Ctrl+C`、`Ctrl + C` 以及 Left/Right 修饰键规则正确复用
- 新增默认开启的当前用户开机自启；注册表命令使用绝对 EXE 路径和 `--background`，路径移动后自动修复，关闭时删除，卸载器清理残留值
- `--background` 启动只创建托盘与监听，不主动显示主窗口；视觉验收模式明确跳过真实注册表
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
- 主界面从程序集动态显示版本；更新器和安装器不再维护独立默认版本，正式包统一从 `GlyphEcho.csproj` 传入当前版本
- 发布流水线除 `update-lite.json`、`update-full.json` 外复制生成兼容 `update.json`，内容与 Lite 清单字节一致

## 修改文件

- Win10 图标与目录 UI：`App.xaml`、`MainWindow.xaml`、`RuleEditorWindow.xaml`、`UpdateWindow.xaml`
- 目录逻辑与规范化：`App.xaml.cs`、`MainWindow.xaml.cs`、`KeyboardHook.cs`
- 回归与视觉验收：`GlyphEcho.SmokeTests/Program.cs`、`VisualQaRunner.cs`
- 开机自启与安装器：`StartupRegistration.cs`、`installer/GlyphEcho.iss`
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
$env:KEYOVERLAY_DATA_DIR = ".\temp\visual-qa-data-catalog-batch-final"
.\bin\Release\net8.0-windows\GlyphEcho.exe --capture-ui ".\temp\ui-captures-catalog-batch-final"
```

- Windows 10 22H2（构建 19045）真实截图通过：侧栏与删除图标正常显示，3 项多选高亮、计数和删除居右布局正确，自启开关默认勾选且布局无重叠
- `temp/ui-captures-startup-final/ui-capture-status.json` 为 success，共 15 张 `PrintWindow` 截图
- 10 项 Smoke Tests 全通过，覆盖按键目录查重/批量删除、自启命令引用和后台参数；更新器路径穿越保护通过
- 本机 Inno Setup 6.7.3 已成功编译 Full/Lite 安装器；两个 Setup 的 Windows 文件版本均为 `0.4.0.0`
- 0.4.0 本地预览包（非正式 Release）：`temp/preview/GlyphEcho-0.4.0-Lite-local-23028564.zip`，296,968 字节，SHA-256 `DD0DDFAA2ED7E68F2057AB73A7274209BDF3486DF447B3FC580C2977A3544FFF`
- 0.4.0 预览 EXE 验收：`temp/preview-ui-captures-040/ui-capture-status.json` 为 success，共 15 张截图

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
- 0.3.1 本地预览包：`temp/preview/GlyphEcho-0.3.1-Lite-local-23028564.zip`，非正式 Release，285,290 字节，SHA-256 `166A3715FA8F5DAF88F165729F1C5DD0B87A375C4A3706E2A68647FFD815C6FE`
- 0.3.1 后台 UI 验收：`temp/preview-ui-captures-031/ui-capture-status.json` 为 success；13 张真实窗口截图均由 `PrintWindow` 捕获，Acrylic 与 frame HRESULT 均为 0，双屏右下偏移位置保持正确
- 0.3.1 本地发布 ZIP 展开验证：Lite 285,290 字节、SHA-256 `458963A064ED7ADECE095E22E9DC4F9C699A9A298413CABB23FDC0AAFA36F106`、7 项；Full 99,550,821 字节、SHA-256 `1926D1C58DA4B6556CA6CCA2E24F598953E21E94201977B3BD160EA60453FDDA`、466 项；通道标记分别为 `lite`/`full`，两个 EXE 文件版本均为 `0.3.1.0`
- 0.3.1 云端流水线 `32871254644` 在 2 分 22 秒内成功；Release 为非草稿、非预发布，包含四个正式包、`SHA256SUMS.txt`、`update.json`、`update-lite.json`、`update-full.json` 共八项资产，兼容清单与 Lite 清单的 GitHub SHA-256 digest 和大小一致
- 简单发布检查确认 GitHub `releases/latest` 返回 `0.3.1`；按用户指示停止大文件下载和客户端公钥深度验签
- 0.4.0 本地发布候选四包均已生成：Full Setup 73,454,156 字节、Lite Setup 2,597,139 字节、Full ZIP 102,743,180 字节、Lite ZIP 296,968 字节；两个 ZIP 分别含 466/7 项，通道为 `full`/`lite`，主程序和更新器文件版本均为 `0.4.0.0`
- 0.4.0 候选包 SHA-256：Full Setup `806C7F8F2C0BBFDC7E97D91B01AE00A447CDA383C91F8A416C9F4DDCFCADD6D5`、Lite Setup `D40C62C0FACBDEE592EDBBADA8F19720AAA8D44B03EE8128C4151E637711DA9C`、Full ZIP `67A383DCDA6890BFEEAD614A7FEEB42FDECDC3451A3971D5F4532842610D51F7`、Lite ZIP `EB92FEF6F6A3A0E243BED16111D7E49D79163C22E9F39D1A5AE1A495ECE5CFCF`
- 0.4.0 云端流水线 `32923875405` 在 2 分 24 秒内成功；Release 为非草稿、非预发布，包含四个正式包、`SHA256SUMS.txt`、`update.json`、`update-lite.json`、`update-full.json` 共八项资产，GitHub `latest` 已指向 0.4.0
- 线上 Lite/Full 清单均通过客户端内置 RSA 公钥的 legacy 与 V2 签名验证；`update.json` 与 `update-lite.json` 的 GitHub digest 和大小一致，`SHA256SUMS.txt` 覆盖四个正式包
- 上一正式版 0.3.1 原始 Lite 客户端在同盘 stage 条件下完成真实升级：发现更新、下载、验签、包预检、更新器替换均成功，最终 EXE 文件版本为 `0.4.0.0`；证据为 `temp/upgrade-031-to-040/upgrade-status-same-volume.json`
- 跨卷回归稳定失败：目标目录在 D:、系统 `%TEMP%` 在 C: 时，0.3.1 更新器日志为 `Source and destination path must have identical roots`；0.4.0 的 `GlyphEcho.Updater/UpdaterProgram.cs` 仍用 `Path.GetTempPath()` 创建 stage，问题尚未修复

## 尚未验证

- 未在本轮自动测试窗口内人工推动左右摇杆、按真实 M1-M4；XInput 连接和静止状态已确认
- 飞智黑武士 4 Pro 当前只暴露标准 XInput 字段；原生 M1-M4 仍无可验证协议。需在飞智空间站将背键映射为 F13-F16 后实测

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
- 用户已明确授权提交、推送、创建 `0.4.0` 标签和 Release；不得覆盖既有 0.3.x 资产，该授权不自动延续到后续无关版本

## 维护规则

每次可交付改动后更新日期、提交基线、验证命令、未验证项和授权边界。事实以 Git、源码、构建输出、状态 JSON 和线上 API 为准；未联网核验的远程状态必须明确标记。
