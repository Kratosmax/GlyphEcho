#define MyAppName "GlyphEcho"
#ifndef APP_VERSION
  #define APP_VERSION "0.2.0"
#endif
#ifndef MODE
  #define MODE "Lite"
#endif
#define MyAppPublisher "Kratosmax"
#define MyAppExeName "GlyphEcho.exe"

[Setup]
AppId={{B65E5A8E-0D99-4A17-8E77-GLYPHECHO020}
AppName={#MyAppName}
AppVersion={#APP_VERSION}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\GlyphEcho
DefaultGroupName=GlyphEcho
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesInstallIn64BitMode=x64
OutputDir=..\temp\release-assets
OutputBaseFilename=GlyphEcho-{#APP_VERSION}-{#MODE}-Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
ChangesAssociations=no

[Files]
Source: "..\temp\package\{#MODE}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\GlyphEcho"; Filename: "{app}\{#MyAppExeName}"
Name: "{userdesktop}\GlyphEcho"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标："

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 GlyphEcho"; Flags: nowait postinstall skipifsilent
