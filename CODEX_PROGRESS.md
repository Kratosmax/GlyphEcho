# GlyphEcho 接续记录

## 接手校准

```powershell
git status --short
git branch --show-current
git remote -v
git log -1 --oneline
Get-Content .\GlyphEcho.csproj
```

版本唯一来源是 `GlyphEcho.csproj` 的 `<Version>`，当前为 `0.1.0`。当前默认分支为 `master`；远程仓库状态需以 `git remote -v` 和 GitHub API 实时核验。

## 当前快照

- 最后本地核验：2026-08-25
- 当前工作：准备创建并推送新的 GitHub 仓库
- 本地提交基线：`a4aef23 chore: finish GlyphEcho branding`
- 线上 Release：尚未创建，不能视为已发布
- 已验证：Release 构建、真实 EXE 启动 3 秒后关闭、本地预览包生成

## 待办与阻塞

- 已获授权：创建新的 GitHub 仓库并推送项目。
- 待确认：仓库可见性（按发布规范默认建议公开）和最终仓库名（建议 `GlyphEcho`）。
- 未做：GitHub Actions、正式 Release、安装器、自动更新签名清单。
- 未验证：飞智黑武士 4 Pro 的专属 HID 报告映射，需要真实设备。

## 验证矩阵

```powershell
dotnet build .\GlyphEcho.csproj --configuration Release --disable-build-servers --no-restore
dotnet publish .\GlyphEcho.csproj --configuration Release --runtime win-x64 --self-contained false --output .\temp\preview\GlyphEcho-0.1.0
git diff --check
git status --short
```

## 不得破坏的约束

- 不把 `PathEcho`、`PinNote` 或用户配置纳入本仓库。
- 不凭猜测硬编码飞智设备的 VID/PID 或 HID 报告布局。
- 不提交密钥、令牌、代理凭据、`bin/obj/temp` 和日志。
- 版本使用 SemVer 形式，不使用 `v1/v2/v3` 产品大版本名。
- 创建仓库、推送、打标签、创建 Release 分开记录并核验。

## 维护规则

每次可交付改动后更新本文件的日期、提交、验证命令、线上状态和未验证项。所有事实以 Git、源码、构建输出和线上 API 为准；未联网核验的远程状态必须明确写“未核验”。
