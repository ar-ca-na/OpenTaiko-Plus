@echo off
rem This file must stay ASCII-only. cmd reads .bat with the OS code page,
rem so Japanese text here would be mangled.
setlocal
chcp 65001 >nul

if not exist "%~dp0OpenTaiko.exe" (
  echo OpenTaiko.exe not found next to this file.
  echo Put this .bat in the same folder as OpenTaiko.exe.
  pause
  exit /b 1
)

if "%~1"=="" (
  echo.
  echo   Drag ^& drop .tja files onto this file to make mp4.
  echo   The mp4 is written next to the .tja.
  echo.
  pause
  exit /b 0
)

echo.
echo   1P difficulty: easy / normal / hard / oni / edit
echo   (just press Enter for oni)
set "D1="
set /p "D1=  1P > "
echo.
echo   2P difficulty: same words, or "none" to play alone
echo   (just press Enter for none)
set "D2="
set /p "D2=  2P > "

set "OPT="
if not "%D1%"=="" set "OPT=%OPT% --difficulty %D1%"
if "%D2%"=="" (set "OPT=%OPT% --difficulty2 none") else (set "OPT=%OPT% --difficulty2 %D2%")

:loop
if "%~1"=="" goto done
echo.
echo === %~nx1 ===
"%~dp0OpenTaiko.exe" --export "%~1" --out "%~dpn1.mp4"%OPT%
shift
goto loop

:done
echo.
echo All done.
pause
