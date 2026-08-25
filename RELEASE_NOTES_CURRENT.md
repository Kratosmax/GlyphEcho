# GlyphEcho 0.3.1

修复 0.3.0 发布资产缺少通用 Lite 更新清单的问题；应用功能与 0.3.0 一致。

## 修复

- 新增兼容入口 `update.json`，内容与已签名的 `update-lite.json` 完全一致
- 保留 `update-lite.json` 和 `update-full.json` 专用通道，所有线路继续执行相同的签名、哈希、版本、大小和包结构校验
- 0.3.0 用户可直接安装本版，现有 `%LOCALAPPDATA%\GlyphEcho` 设置保持不变

## 下载

- `GlyphEcho-0.3.1-Full-Setup.exe` / `GlyphEcho-0.3.1-Lite-Setup.exe`
- `GlyphEcho-0.3.1-Full.zip` / `GlyphEcho-0.3.1-Lite.zip`

Lite 包需要 .NET 8 Desktop Runtime；Full 包已包含运行时。
