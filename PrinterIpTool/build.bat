@echo off
rem PrinterIpTool build script (ASCII only) - by RenWoXing
cd /d "%~dp0"

set DIST=%~dp0dist
set SRC=PrinterIpTool.csproj

echo === STEP 1: locate build engine ===
set ENG=
if exist "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" set ENG=C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe
if not defined ENG if exist "C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe" set ENG=C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe
if not defined ENG if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" set ENG=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe
if not defined ENG if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" set ENG=%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe
if not defined ENG if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" set ENG=%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe

if not defined ENG (
  echo ERROR: build engine not found. Install Visual Studio with .NET desktop workload.
  pause
  exit /b 1
)
echo Engine: %ENG%

echo === STEP 2: build Release ===
"%ENG%" %SRC% /p:Configuration=Release /p:Platform=AnyCPU /nologo /v:m
if errorlevel 1 (
  echo BUILD FAILED
  pause
  exit /b 1
)

echo === STEP 3: pack green package ===
if not exist "%DIST%" mkdir "%DIST%"
copy /y "bin\Release\PrinterIpTool.exe" "%DIST%\" >nul
if exist "bin\Release\PrinterIpTool.exe.config" copy /y "bin\Release\PrinterIpTool.exe.config" "%DIST%\" >nul
copy /y "app.ico" "%DIST%\" >nul
copy /y "*.txt" "%DIST%\" >nul

echo.
echo BUILD OK - green package at: %DIST%
dir /b "%DIST%"
pause
