; Keep Awake - Inno Setup installer script
; Copies the self-contained published EXE + DLLs to the user's local programs folder.
; No elevation required: PrivilegesRequired=lowest installs per-user.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define AppName      "Keep Awake"
#define AppPublisher "Evan Trowbridge"
#define AppGUID      "2b2d239a-3948-429b-b45a-ba15ad354f72"
#define AppExe       "Keep Awake.exe"

[Setup]
AppId={{{#AppGUID}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppSupportURL=https://github.com/evantrowbridge/Arduino-Keep-Awake
DefaultDirName={localappdata}\Programs\{#AppName}
OutputBaseFilename=KeepAwake-Setup-{#AppVersion}
OutputDir=output
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
MinVersion=10.0.17763
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{userstartmenu}\Programs\{#AppName}"; Filename: "{app}\{#AppExe}"

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
