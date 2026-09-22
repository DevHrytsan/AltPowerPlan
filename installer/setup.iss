#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#ifndef SourceDir
  #define SourceDir "..\dist\portable"
#endif

#ifndef OutputBaseFilename
  #define OutputBaseFilename "AltPowerPlan-v" + AppVersion + "-Setup"
#endif

[Setup]
AppId={{B95B30D1-2B7A-44C1-8457-4E2F1F8DA5D6}
AppName=AltPowerPlan
AppVersion={#AppVersion}
AppVerName=AltPowerPlan v{#AppVersion}
AppPublisher=DevHrytsan
AppPublisherURL=https://github.com/DevHrytsan/AltPowerPlan
AppSupportURL=https://github.com/DevHrytsan/AltPowerPlan/issues
AppUpdatesURL=https://github.com/DevHrytsan/AltPowerPlan/releases
DefaultDirName={autopf}\AltPowerPlan
DefaultGroupName=AltPowerPlan
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir=..\release-artifacts
OutputBaseFilename={#OutputBaseFilename}
SetupIconFile=..\AltPowerPlan.App\Assets\icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible arm64
CloseApplications=yes
RestartApplications=no
PrivilegesRequiredOverridesAllowed=dialog commandline

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[CustomMessages]
english.DotNet10RequiredPrompt=AltPowerPlan requires Microsoft .NET Desktop Runtime 10.0 (x64) to run.%n%nWould you like to download and install it now from Microsoft?
ukrainian.DotNet10RequiredPrompt=Для роботи AltPowerPlan потрібен Microsoft .NET Desktop Runtime 10.0 (x64).%n%nБажаєте завантажити та встановити його зараз з офіційного сайту Microsoft?

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start AltPowerPlan with Windows"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "portable.dat"

[Icons]
Name: "{group}\AltPowerPlan"; Filename: "{app}\AltPowerPlan.exe"; IconFilename: "{app}\Assets\icon.ico"
Name: "{group}\{cm:UninstallProgram,AltPowerPlan}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\AltPowerPlan"; Filename: "{app}\AltPowerPlan.exe"; IconFilename: "{app}\Assets\icon.ico"; Tasks: desktopicon
Name: "{userstartup}\AltPowerPlan"; Filename: "{app}\AltPowerPlan.exe"; Tasks: startupicon

[Run]
Filename: "{app}\AltPowerPlan.exe"; Description: "{cm:LaunchProgram,AltPowerPlan}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
function IsDotNet10DesktopRuntimeInstalled(): Boolean;
var
  FindRec: TFindRec;
  SharedFxDir: String;
  RegNames: TArrayOfString;
  I: Integer;
begin
  Result := False;

  // 1. Check WOW6432Node registry (standard location written by .NET x64 runtime installer)
  if RegGetValueNames(HKLM32, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', RegNames) then
  begin
    for I := 0 to GetArrayLength(RegNames) - 1 do
    begin
      if (Pos('10.', RegNames[I]) = 1) then
      begin
        Result := True;
        Exit;
      end;
    end;
  end;

  // 2. Check 64-bit native registry view
  if RegGetValueNames(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', RegNames) then
  begin
    for I := 0 to GetArrayLength(RegNames) - 1 do
    begin
      if (Pos('10.', RegNames[I]) = 1) then
      begin
        Result := True;
        Exit;
      end;
    end;
  end;

  // 3. Fallback: Check filesystem folder for Microsoft.WindowsDesktop.App\10.*
  SharedFxDir := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if DirExists(SharedFxDir) then
  begin
    if FindFirst(SharedFxDir + '\10.*', FindRec) then
    begin
      try
        repeat
          if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0) and
             (FindRec.Name <> '.') and (FindRec.Name <> '..') then
          begin
            Result := True;
            Exit;
          end;
        until not FindNext(FindRec);
      finally
        FindClose(FindRec);
      end;
    end;
  end;
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
#ifndef Standalone
  if not IsDotNet10DesktopRuntimeInstalled() then
  begin
    if MsgBox(CustomMessage('DotNet10RequiredPrompt'), mbConfirmation, MB_YESNO) = idYes then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/10.0', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
    Result := False;
  end;
#endif
end;
