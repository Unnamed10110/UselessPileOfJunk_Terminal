@echo off
setlocal
cd /d "%~dp0"

echo Building MSI installer (self-contained Release win-x64)...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Build-Msi.ps1" %*
if errorlevel 1 (
  echo.
  echo MSI build failed.
  exit /b 1
)

echo.
exit /b 0
