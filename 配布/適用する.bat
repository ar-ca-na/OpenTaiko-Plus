@echo off
rem This file must stay ASCII-only. cmd reads .bat with the OS code page.
rem All messages are printed by apply.ps1 instead.
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0apply.ps1" %*
if errorlevel 1 exit /b 1
exit /b 0
