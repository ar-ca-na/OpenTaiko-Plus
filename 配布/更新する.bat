@echo off
rem This file must stay ASCII-only. cmd reads .bat with the OS code page.
rem The update may overwrite this very file, so the call and exit stay on one line
rem (cmd reads a .bat line by line while it runs).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scorevideo_update.ps1" & exit /b
