@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

if not exist "ObliqueSeasons.exe" (
  call "%~dp0Build-EXE.bat"
  if errorlevel 1 exit /b 1
)

start "" "%~dp0ObliqueSeasons.exe"
endlocal
