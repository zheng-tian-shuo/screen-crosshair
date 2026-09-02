@echo off
REM Double-click entry point. All real work happens in build.ps1.
REM This file stays pure ASCII because cmd.exe reads .bat with the console codepage.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
echo.
pause
