@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall.ps1"
set "UNINSTALL_EXIT=%ERRORLEVEL%"
echo.
if not "%UNINSTALL_EXIT%"=="0" echo Uninstall failed with code %UNINSTALL_EXIT%.
pause
exit /b %UNINSTALL_EXIT%
