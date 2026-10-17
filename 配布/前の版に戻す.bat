@echo off
rem This file must stay ASCII-only. cmd reads .bat with the OS code page.
rem Goes back to the release just before the installed one (scorevideo_version.txt).
rem The call and exit stay on one line (cmd reads a .bat line by line while it runs).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scorevideo_update.ps1" -Tag previous & exit /b
