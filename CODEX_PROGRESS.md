# GlyphEcho 接续记录

## 接手校准

```powershell
git status --short
git branch --show-current
git remote -v
git log -1 --oneline
Get-Content .\GlyphEcho.csproj
```

版本唯一来源是 `GlyphEcho.csproj` 的 `<Version>`，当前为 `0.2.0`。当前默认分支为 `main`；远程仓库为 `git@github.com:Kratosmax/GlyphEcho.git`。

## 当前快照

- 最后本地核验：2026-08-25
- 当前工作：维护网络与更新页、按键过滤和多屏定位
- 本地提交基线：待提交（发布记录修正）
- 线上 Release：`0.2.0`，https://github.com/Kratosmax/GlyphEcho/releases/tag/0.2.0
- 已验证：本地构建、真实 EXE 启动、线上清单字段和 RSA 签名、网络页构建、多屏定位代码编译

## 待办与阻塞

- 已完成：创建公开仓库 `Kratosmax/GlyphEcho`，推送 `main` 和标签 `0.1.0`。
- 已完成：四资产脚本、Inno Setup 定义、Actions 工作流、签名清单校验入口、独立更新器和回滚流程。
- 已完成：`UPDATE_SIGNING_PRIVATE_KEY` 已写入 GitHub Actions Secret。
- 已完成：推送 `0.2.0` 标签、Actions 首次运行和线上四资产核验。
- 本轮未发布新 Release；改动待下一个版本标签发布。
- 未验证：飞智黑武士 4 Pro 的专属 HID 报告映射，需要真实设备。

## 验证矩阵

```powershell
dotnet build .\GlyphEcho.csproj --configuration Release --disable-build-servers --no-restore
dotnet publish .\GlyphEcho.csproj --configuration Release --runtime win-x64 --self-contained false --output .\temp\preview\GlyphEcho-0.2.0
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
