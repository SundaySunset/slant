@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

if not exist "Slant.exe" (
  call "%~dp0Build-EXE.bat"
  if errorlevel 1 exit /b 1
)

start "" "%~dp0Slant.exe" --window
endlocal
