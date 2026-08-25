# GlyphEcho 0.2.0

完整发布链版本。

## 新增

- Full/Lite ZIP 便携版
- Full/Lite Inno Setup 安装器
- GitHub Actions 标签发布流水线
- RSA 签名的 Full/Lite 更新清单
- 更新清单签名、版本、大小和 SHA-256 校验
- 独立更新器：等待主进程退出、暂存解压、路径检查、替换失败回滚
- 托盘中的更新检查和下载安装入口

## 下载选择

- `GlyphEcho-0.2.0-Full-Setup.exe`：自包含安装器，不要求预装 .NET 8。
- `GlyphEcho-0.2.0-Lite-Setup.exe`：需要 .NET 8 Desktop Runtime。
- `GlyphEcho-0.2.0-Full.zip`：自包含便携版。
- `GlyphEcho-0.2.0-Lite.zip`：依赖 .NET 8 Desktop Runtime 的便携版。

## 安全说明

更新清单使用仓库 Actions Secret 中的 RSA 私钥签名，客户端内置公钥并在下载前验证清单、版本、通道、大小和哈希。更新失败时保留备份并尝试回滚；若当前目录权限或文件占用阻止替换，现有版本不会被静默覆盖。

## 已知限制

飞智黑武士 4 Pro 的厂商专属 HID 报告映射仍需要连接真实设备后确认；通用 HID 入口不会假定固定 VID/PID 或报告布局。
