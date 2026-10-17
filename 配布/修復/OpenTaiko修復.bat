@echo off
rem This file must stay ASCII-only. cmd reads .bat with the OS code page.
rem Puts OpenTaiko.exe back after security software deleted it, then guides adding an exclusion.
rem Double-click it inside the OpenTaiko folder, or drop the OpenTaiko folder onto it.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0opentaiko_repair.ps1" "%~1"
