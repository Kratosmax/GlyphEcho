# GlyphEcho 0.2.1

修复更新检查、按键目录和多屏展示定位。

## 修复

- 更新清单 JSON 字段大小写兼容，修复“产品或通道不匹配”误报
- 新增“网络与更新”页面，提供通道选择、连通性测试和签名检查状态
- 纯 Shift 文本输入（如 `Shift + 1`、`Shift + /`、`Shift + OemQuotes`）不再写入快捷键目录
- 规范化 `OemQuotes`、`OemBackslash` 等键名
- 修复多屏和高 DPI 下叠加层飘到屏幕中间的问题

## 下载

- `GlyphEcho-0.2.1-Full-Setup.exe` / `GlyphEcho-0.2.1-Lite-Setup.exe`
- `GlyphEcho-0.2.1-Full.zip` / `GlyphEcho-0.2.1-Lite.zip`

更新清单继续使用 RSA 签名，并校验产品、通道、版本、大小和 SHA-256。
