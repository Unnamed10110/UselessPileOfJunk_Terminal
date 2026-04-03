@echo off
setlocal
cd /d "%~dp0"

echo Publishing self-contained Release win-x64 to .\publish\
echo.

dotnet publish "src\UselessTerminal\UselessTerminal.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -o "%~dp0publish"

if errorlevel 1 (
  echo.
  echo Build failed.
  exit /b 1
)

echo.
echo Success: %~dp0publish\UselessTerminal.exe
exit /b 0
