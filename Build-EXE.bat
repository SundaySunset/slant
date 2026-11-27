@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

set "NO_PAUSE=0"
if /I "%~1"=="--no-pause" set "NO_PAUSE=1"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] .NET 8 SDK ^(dotnet^) not found.
  echo Please install .NET 8 SDK ^(https://dotnet.microsoft.com/download/dotnet/8.0^) and retry.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

echo Building Slant.exe ^(.NET 8 Windows App^)...
set "BUILT=0"

if exist "%USERPROFILE%\.nuget\packages" (
  dotnet publish "src\Slant.csproj" -c Release -r win-x64 --self-contained false --source "%USERPROFILE%\.nuget\packages" -p:RestorePackagesPath="%USERPROFILE%\.nuget\packages" -o "src\publish"
  if not errorlevel 1 set "BUILT=1"
)

if "%BUILT%"=="0" (
  echo Trying online NuGet restore...
  dotnet publish "src\Slant.csproj" -c Release -r win-x64 --self-contained false -o "src\publish"
  if not errorlevel 1 set "BUILT=1"
)

if "%BUILT%"=="0" (
  echo [ERROR] Build failed.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

if not exist "src\publish\Slant.exe" (
  echo [ERROR] Published executable not found: src\publish\Slant.exe
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

copy /Y "src\publish\Slant.exe" "Slant.exe" >nul
if errorlevel 1 (
  echo [ERROR] Failed to copy Slant.exe. Make sure the app is not currently running.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

rmdir /s /q "src\publish" >nul 2>nul
echo [OK] Slant.exe built successfully.
endlocal
