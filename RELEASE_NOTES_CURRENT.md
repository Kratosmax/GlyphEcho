# GlyphEcho 0.4.0

本版新增按键目录批量管理和当前用户开机自启，并修复 Windows 10 图标兼容与目录查重性能问题。现有 `%LOCALAPPDATA%\GlyphEcho` 设置保持不变。

## 新功能

- 按键目录支持多选、全选当前搜索结果、已选计数和带确认的批量删除
- 新增默认开启的当前用户开机自启；登录 Windows 后在后台启动，不主动弹出主窗口，可在概览页关闭
- 安装版卸载时清理 GlyphEcho 自己的开机自启项

## 修复与优化

- 图标统一使用 Windows 10 自带字体，修复侧栏和删除按钮显示方框的问题
- 删除按钮统一居右，选中项使用完整行高亮
- 按键查重改为内存索引，已存在按键不再重复刷新列表或写入配置
- 统一 `Ctrl+C` 与 `Ctrl + C`、Left/Right 修饰键的规则匹配
- 按键目录的启用开关现在立即同步默认规则并保存

## 下载

- `GlyphEcho-0.4.0-Full-Setup.exe` / `GlyphEcho-0.4.0-Lite-Setup.exe`
- `GlyphEcho-0.4.0-Full.zip` / `GlyphEcho-0.4.0-Lite.zip`

Lite 包需要 .NET 8 Desktop Runtime；Full 包已包含运行时。
