@echo off

cd /d "%~dp0"

dotnet build "NoTouchItems.csproj" -c Release

if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo.

echo Deployed notouchitems.dll + modinfo + assets to:

echo   This folder: project root, and
echo   %%APPDATA%%\VintagestoryData\Mods\notouchitems
echo  (Do not use the game install\Mods for dev copies; use VintagestoryData\Mods or your portable data folder.)
echo.

echo After loading a world, check server-main.log for: No Touch Items v... server hooks loaded.

echo Join chat should show the same version prefix as NoTouchConstants.ModVersion in the source.


