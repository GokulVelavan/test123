@echo off
REM =============================================================================
REM Syslog Stack Installer - Build Script
REM =============================================================================
REM Prerequisites:
REM   1. Inno Setup 6.x installed (default: C:\Program Files (x86)\Inno Setup 6)
REM   2. Offline dependencies placed in .\dependencies\ folder
REM   3. Docker image pre-built and saved as .\dependencies\syslog-ng.tar
REM =============================================================================

setlocal enabledelayedexpansion

echo.
echo ============================================
echo   Syslog Stack Installer - Build
echo ============================================
echo.

REM --- Find Inno Setup compiler ---
set "ISCC="
if exist "D:\Apps\Inno Setup 6\ISCC.exe" (
    set "ISCC=D:\Apps\Inno Setup 6\ISCC.exe"
) else if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
) else if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"
) else (
    echo [ERROR] Inno Setup 6 not found!
    echo         Download from: https://jrsoftware.org/isdl.php
    echo         Install to default location and try again.
    exit /b 1
)

echo [OK] Found Inno Setup: %ISCC%

REM --- Validate dependencies ---
echo.
echo Checking dependencies...

set "MISSING=0"

if not exist "dependencies\DockerDesktop-x64.exe" (
    echo [WARN] Missing: dependencies\DockerDesktop-x64.exe
    set "MISSING=1"
)

set "PG_FOUND=0"
for %%f in (dependencies\postgresql-*.exe) do (
    set "PG_FOUND=1"
)
if "!PG_FOUND!"=="0" (
    echo [WARN] Missing: dependencies\postgresql-16.x-x64.exe
    set "MISSING=1"
)

if not exist "dependencies\syslog-ng.tar" (
    echo [WARN] Missing: dependencies\syslog-ng.tar
    set "MISSING=1"
)

if "!MISSING!"=="1" (
    echo.
    echo [WARN] Some dependency files are missing.
    echo        The installer will still build, but installation
    echo        will fail if the required files are not present.
    echo.
    set /p CONTINUE="Continue anyway? (Y/N): "
    if /i not "!CONTINUE!"=="Y" exit /b 1
) else (
    echo [OK] All dependencies found.
)

REM --- Create Output directory ---
if not exist "Output" mkdir Output

REM --- Build the installer ---
echo.
echo Building installer...
echo.
"%ISCC%" Setup.iss

if errorlevel 1 (
    echo.
    echo [ERROR] Build failed!
    exit /b 1
)

echo.
echo ============================================
echo   Build successful!
echo   Output: Output\SyslogStack-Setup-1.0.0.exe
echo ============================================
echo.

endlocal
