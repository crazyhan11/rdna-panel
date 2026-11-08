; ---- RadeonTune installer (NSIS 3) ----
Unicode true
!define APPNAME "RDNA Panel"
!define VERSION "1.3.0-rdna2"
Name "${APPNAME} ${VERSION}"
OutFile "RDNAPanel-Setup-${VERSION}.exe"
InstallDir "$LOCALAPPDATA\Programs\RDNAPanel"
InstallDirRegKey HKCU "Software\RDNA Panel" "InstallDir"
RequestExecutionLevel user
SetCompressor zlib
SetCompressorDictSize 32
Icon "..\..\app.ico"
UninstallIcon "..\..\app.ico"

!include "MUI2.nsh"
!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\RDNAPanel.exe"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "English"

Section "Install"
  ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\RDNA Panel" "InstallLocation"
  StrCmp $0 "" +6
  StrCmp $0 "$INSTDIR" +5
  IfSilent 0 +2
  Abort
  MessageBox MB_YESNO|MB_ICONQUESTION "RDNA Panel is already installed at:$\r$\n$0$\r$\n$\r$\nInstall a second copy anyway?" IDYES +2
  Abort
  SetShellVarContext current

  CreateDirectory "$INSTDIR"
  SetOutPath "$INSTDIR"
  ; 先清掉旧版本的程序文件（保留 data 配置）
  RMDir /r "$INSTDIR\cs"
  RMDir /r "$INSTDIR\runtimes"
  RMDir /r "$INSTDIR\assets"
  Delete "$INSTDIR\*.dll"
  Delete "$INSTDIR\RDNAPanel.exe"
  File /r "..\portable\*.*"
  ; 保留用户数据
  IfFileExists "$INSTDIR\data\settings.json" +2 0
  CreateDirectory "$INSTDIR\data"

  CreateShortcut "$SMPROGRAMS\RDNA Panel.lnk" "$INSTDIR\RDNAPanel.exe" "" "$INSTDIR\RDNAPanel.exe" 0
  CreateShortcut "$DESKTOP\RDNA Panel.lnk" "$INSTDIR\RDNAPanel.exe" "" "$INSTDIR\RDNAPanel.exe" 0
  WriteRegStr HKCU "Software\RDNA Panel" "InstallDir" "$INSTDIR"
  WriteUninstaller "$INSTDIR\uninstall.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\RDNA Panel" "DisplayName" "${APPNAME}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\RDNA Panel" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\RDNA Panel" "Publisher" "RadeonTune"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\RDNA Panel" "DisplayIcon" "$INSTDIR\RDNAPanel.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\RDNA Panel" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\RDNA Panel" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\RDNA Panel" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\RDNA Panel" "NoRepair" 1
SectionEnd

Section "Uninstall"
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "RadeonTune"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\RDNA Panel"
  DeleteRegKey HKCU "Software\RDNA Panel"
  DeleteRegKey HKCU "Software\RadeonTune"
  Delete "$SMPROGRAMS\RDNA Panel.lnk"
  Delete "$DESKTOP\RDNA Panel.lnk"
  RMDir /r "$INSTDIR"
SectionEnd
