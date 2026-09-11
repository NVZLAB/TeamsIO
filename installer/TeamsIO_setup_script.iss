#define PublishDir SourcePath + "\..\bin\Release\net8.0-windows\win-x64\publish"
#define AppExe PublishDir + "\TeamsIO.exe"
#define FileVersion GetVersionNumbersString(AppExe)
#define AppVersion Copy(FileVersion, 1, RPos(".", FileVersion) - 1)

[Setup]
AppName=TeamsIO
AppVersion={#AppVersion}
AppId=NVZLAB.TeamsIO
SetupIconFile={#SourcePath}\..\assets\TeamsIO.ico
DefaultDirName={localappdata}\Programs\TeamsIO
DefaultGroupName=TeamsIO
UninstallDisplayIcon={app}\TeamsIO.exe
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
OutputDir={#SourcePath}\Output
OutputBaseFilename=TeamsIO_Setup_{#SetupSetting("AppVersion")}
Compression=lzma2
SolidCompression=yes

[Files]
Source: "{#PublishDir}\TeamsIO.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\appsettings.example.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\docs\SETUP.md"; DestDir: "{app}\docs"; Flags: ignoreversion

[Icons]
Name: "{userdesktop}\TeamsIO"; Filename: "{app}\TeamsIO.exe"; Parameters: "--window"
Name: "{userprograms}\TeamsIO"; Filename: "{app}\TeamsIO.exe"; Parameters: "--window"

[Run]
Filename: "{app}\TeamsIO.exe"; Parameters: "--window"; \
    Description: "Launch TeamsIO"; \
    Flags: nowait postinstall skipifsilent
