@echo off
setlocal enabledelayedexpansion
set MOD_NAME=DeckAdvisor
set SCRIPT_DIR=%~dp0

:: Locate STS2
if not "%STS2_PATH%"=="" goto found_sts2

for /f "tokens=2*" %%a in ('reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 2868840" /v InstallLocation 2^>nul') do set STS2_PATH=%%b
if exist "%STS2_PATH%\data_sts2_windows_x86_64" goto found_sts2

for /f "tokens=2*" %%a in ('reg query "HKCU\Software\Valve\Steam" /v SteamPath 2^>nul') do set STEAM_PATH=%%b
set STEAM_PATH=%STEAM_PATH:/=\%
if exist "%STEAM_PATH%\steamapps\common\Slay the Spire 2" (
    set STS2_PATH=%STEAM_PATH%\steamapps\common\Slay the Spire 2
    goto found_sts2
)

echo ERROR: Could not find Slay the Spire 2. Set STS2_PATH environment variable.
exit /b 1

:found_sts2
set MODS_DIR=%STS2_PATH%\mods

:: Locate Godot 4.5.1
set GODOT_EXE=
if exist "%SCRIPT_DIR%Directory.Build.props" (
    for /f "tokens=2 delims=<>" %%a in ('findstr /i "GodotPath" "%SCRIPT_DIR%Directory.Build.props"') do set GODOT_EXE=%%a
)
if not exist "%GODOT_EXE%" (
    set GODOT_EXE=%SCRIPT_DIR%..\AgentTheSpire\godot\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe
)
if not exist "%GODOT_EXE%" (
    echo WARNING: Godot 4.5.1 not found - skipping .pck export.
    set GODOT_EXE=
)

echo =^> STS2 path: %STS2_PATH%
echo =^> Mods dir:  %MODS_DIR%

:: Build .dll
echo =^> Building %MOD_NAME%.dll...
cd /d "%SCRIPT_DIR%"
dotnet publish -c Release -p:Sts2Path="%STS2_PATH%" -o "%SCRIPT_DIR%dist\%MOD_NAME%"
if errorlevel 1 ( echo ERROR: Build failed. & exit /b 1 )

:: Export .pck
if not "%GODOT_EXE%"=="" (
    echo =^> Exporting .pck...
    "%GODOT_EXE%" --headless --export-pack "BasicExport" "%SCRIPT_DIR%dist\%MOD_NAME%\%MOD_NAME%.pck"
)

:: Copy manifest
copy /y "%SCRIPT_DIR%%MOD_NAME%.json" "%SCRIPT_DIR%dist\%MOD_NAME%\" >nul
copy /y "%SCRIPT_DIR%mod_manifest.json" "%SCRIPT_DIR%dist\%MOD_NAME%\" >nul

:: Deploy
if exist "%MODS_DIR%" (
    echo =^> Deploying to %MODS_DIR%\%MOD_NAME%\
    if not exist "%MODS_DIR%\%MOD_NAME%" mkdir "%MODS_DIR%\%MOD_NAME%"
    xcopy /e /y "%SCRIPT_DIR%dist\%MOD_NAME%\*" "%MODS_DIR%\%MOD_NAME%\" >nul
    echo =^> Done! Enable DeckAdvisor in the game's mod list.
) else (
    echo =^> Mods directory not found. Package is at: %SCRIPT_DIR%dist\%MOD_NAME%\
)
