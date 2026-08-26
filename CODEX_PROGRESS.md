# GlyphEcho 接续记录

## 接手校准

```powershell
git status --short --branch
git branch --show-current
git remote -v
git log -1 --oneline
Get-Content .\GlyphEcho.csproj
```

版本唯一来源是 `GlyphEcho.csproj` 的 `<Version>`，当前版本为 `0.5.1`。默认分支为 `main`，远程仓库为 `https://github.com/Kratosmax/GlyphEcho.git`。

## 当前快照

- 最后本地核验：2026-08-26
- 提交基线：`7a4445f`（0.5.1 标签提交）
- 当前状态：0.5.1 已发布并完成线上资产、双签名和 latest 指向核验
- 远程状态：`main`、`0.5.1` 标签和 Release 已推送；Actions `32946609866` 成功，GitHub `latest` 已指向 0.5.1
- 当前工作：配置、输入热路径、共享规则索引、更新检查互斥与诊断、搜索防抖、更新恢复和发布失败检测已发布
- 当前正式版本为 `0.5.1`；版本、标签、Release 正文、八项资产和更新清单一致

## 当前待办

- 当前没有已授权但未完成的开发或发布任务，等待用户后续需求
- 0.3.1/0.4.0 非系统盘便携用户仍需一次手动覆盖；README 与 0.5.0 Release Notes 已加入过渡说明，不得覆盖既有历史资产

## 本轮实现

### 0.5.1 可靠性与性能修复

- 全局按键目录只保留一份规范化字典；默认规则直接引用共享索引，旧配置中的 `DefaultRule.KeyRules` 仅迁移一次后清空
- 应用规则运行时只缓存稀疏覆盖，不再为每个应用复制完整目录；规则编辑器也只保留一份可编辑副本
- 10,000 项目录的应用规则首次解析从约 18.5ms、4.13MiB 降至约 0.72ms、4KiB；随后缓存命中 26.2ns/次、0 B/次
- 页面、托盘和后台更新检查统一经过主窗口运行状态，同一时间只允许一个检查；后台异常写入 `%LocalAppData%\GlyphEcho\logs\update-check-failed.log`
- 按键目录搜索使用 500ms `DispatcherTimer` 防抖，输入期间不再反复筛选和排序完整目录
- 图标生成先写入项目 `temp/icon-generation`，像素一致时不覆盖源 PNG；连续两次生成前后 SHA-256 均为 `01F42BC115963B8571359D60260E576B3069865C00B247E1D3D1CEF7A0FAD824`
- 配置保存、键盘钩子、手柄退避、提示重绘、更新器恢复、共享清单验证、CI 与正式打包失败检测已包含在 0.5.1

0.5.1 标签提交文件：

```text
.github/workflows/release.yml
App.xaml.cs
Assets/GlyphEcho.png
CODEX_PROGRESS.md
GamepadHook.cs
GlyphEcho.csproj
GlyphEcho.SmokeTests/Program.cs
GlyphEcho.Updater/GlyphEcho.Updater.csproj
GlyphEcho.Updater/UpdaterProgram.cs
KeyboardHook.cs
MainWindow.xaml.cs
ModePolicy.cs
OverlayQueue.cs
OverlayWindow.Queue.cs
RuleCatalog.cs
RuleEditorWindow.xaml.cs
UpdateManifestValidator.cs
UpdateNetworkSettings.cs
UpdateService.cs
VisualQaRunner.cs
installer/GlyphEcho.iss
scripts/Build-ReleaseAssets.ps1
scripts/Generate-AppIcon.ps1
```

本轮验证：

- 主程序、SmokeTests 和更新器 Release 构建：0 警告、0 错误
- SmokeTests 14/14 通过；更新器 self-test 2/2 通过
- 真实 WPF `--capture-ui` 验收：`temp/review-ui-performance-fixes/ui-capture-status.json` 为 `success=true`，19 张截图、双屏坐标和提示尺寸检查通过
- 0.5.1 正式候选四包已生成：Full Setup 73,468,538 字节、Full ZIP 102,757,697 字节、Lite Setup 2,608,021 字节、Lite ZIP 311,866 字节
- 0.5.1 候选 SHA-256：Full Setup `F3998358EF69FF22733F8BEFC2021B5824F85E47E87636DA4255320273C7900F`、Full ZIP `E02E6923C67D7B79DC80314905BB9640362ECCD18681438251FE05635178CA24`、Lite Setup `C6BE59EFB7450B1BC679791B3CE44EBC9B9D9BC3F0A47F9F497FFF032E633B35`、Lite ZIP `5622B8CE9D172A1B83787A2FAA8E76705158B3F3BC4A12337ED03A61D5AECC2A`
- Lite/Full ZIP 分别为 7/466 项，通道标记与包型一致，主程序和更新器 ProductVersion 均为 0.5.1
- 云端 Actions `32946609866` 在 2 分 16 秒内成功；测试、四包构建、校验和、双签名清单和资产上传均通过
- 0.5.1 Release 为非草稿、非预发布并包含八项资产；`latest` API 返回 0.5.1
- 线上资产：Full Setup 73,467,997 字节、Full ZIP 102,757,109 字节、Lite Setup 2,606,510 字节、Lite ZIP 311,265 字节
- 线上 SHA-256：Full Setup `ED799423B847C1A15B3B18DC5361569F9205AD370CC26A6A1BDF0012C658E189`、Full ZIP `8ABD182EBDEAC9A8E84FA22C47DCC697CA223966782D2B21DDBD20C039C03E27`、Lite Setup `6EC8F391198BDF19BBF09CED52AE97A061F019080ECBDD648D70DBBB21E10D0F`、Lite ZIP `A3DFF0B37C4558FB7952A6AD9E81A5292DDB8C9681128139E6C79991CD05C709`
- 线上 Lite/Full 清单均通过客户端内置 RSA 公钥的 legacy 与 V2 签名验证；`update.json` 与 `update-lite.json` 字节一致，`SHA256SUMS.txt` 覆盖四个正式包
- `git diff --check` 通过；仅有仓库现有 LF/CRLF 策略提示
- Codex 图片查看器仍被 Windows ACL 辅助器故障阻断，本轮未能再次人工放大截图；自动像素、尺寸和位置验收已通过

- 新增深色薄荷绿、天空蓝、琥珀黄和浅色深青、钴蓝、玫红六种提示色板，设置页使用 `×1 / ×2 / ×3` 预览并即时保存
- 提示队列按当前级别实际可见内容合并：低级按按键，中级增加来源，高级再增加功能；不再让不可见的前台应用采样变化生成重复行
- 更新器的 stage 与 backup 改到目标目录同父级，保证事务目录和便携程序位于同一卷
- 版本源、README 和当前版本发布说明统一更新为 0.5.0，并注明旧便携版跨卷更新的一次性手动升级路径

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

0.5.0 候选验证（2026-08-26）：

- `dotnet build .\GlyphEcho.SmokeTests\GlyphEcho.SmokeTests.csproj --configuration Release --disable-build-servers`：0 警告、0 错误
- `dotnet run --project .\GlyphEcho.SmokeTests\GlyphEcho.SmokeTests.csproj --configuration Release --no-build`：11 项全部通过，覆盖按可见内容合并、色板规范化与 WCAG 4.5 对比度
- `dotnet run --project .\GlyphEcho.Updater\GlyphEcho.Updater.csproj --configuration Release --no-restore -- --self-test`：同卷事务替换和 ZIP 路径穿越保护通过
- `scripts/Build-Preview.ps1`：Lite 本地预览包 `temp/preview/GlyphEcho-0.5.0-Lite-local-23028564.zip`，302,721 字节，SHA-256 `F50E559AA8077BDFC2842D2ED0A60E03B8643D8178C56EEFFF4DBBF9A7E947BA`
- `scripts/Build-ReleaseAssets.ps1 -Version 0.5.0`：Full/Lite ZIP 和两个 Setup 均生成，主程序、更新器、安装器版本均为 `0.5.0.0`
- 源码与 Lite 预览真实 WPF `PrintWindow` 验收均为 success，各 17 张截图；包含六色色板页、深色提示、浅色玫红提示、最小窗口、不透明回退和双屏定位
- 当前截图可由程序自动确认尺寸、颜色采样和坐标，但本轮 `view_image` 受 Windows ACL 辅助器故障影响，尚未完成人工观感确认
- Actions `32927973532` 在 3 分 1 秒内成功；Release 为非草稿、非预发布，GitHub `latest` 指向 0.5.0
- 线上 Release 包含 Full/Lite Setup、Full/Lite ZIP、`SHA256SUMS.txt`、`update.json`、`update-lite.json`、`update-full.json` 共八项资产
- 线上 Lite/Full 清单的包大小和 SHA-256 与 GitHub 资产 digest、`SHA256SUMS.txt` 一致；legacy 与 V2 签名均通过客户端内置 RSA 公钥验证
- `update.json` 与 `update-lite.json` 字节一致，Release 正文只包含 0.5.0 当前版本内容
- 原始 0.4.0 Lite 客户端在同卷事务目录下完成真实线上升级：获取清单、签名验证、下载、包预检、更新器替换均成功，最终 EXE 为 `0.5.0.0`；证据为 `temp/upgrade-040-to-050/upgrade-status-final.json`

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

- 由于本轮 Windows ACL 辅助器故障，模型未能直接读取 0.5.0 截图进行人工观感复核；真实窗口捕获、非空像素、尺寸和双屏坐标自动验收已通过
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
- 用户已明确授权并完成 `0.5.1` 的提交、推送、标签和 Release；不得覆盖既有 0.3.x、0.4.0、0.5.0 或 0.5.1 资产，该授权不自动延续到后续版本

## 维护规则

每次可交付改动后更新日期、提交基线、验证命令、未验证项和授权边界。事实以 Git、源码、构建输出、状态 JSON 和线上 API 为准；未联网核验的远程状态必须明确标记。
