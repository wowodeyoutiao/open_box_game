@echo off
setlocal
cd /d "%~dp0"
set "NOPAUSE="
if /I "%~1"=="--no-pause" set "NOPAUSE=1"

set "MSB=E:\DevelopTools\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
set "PROJ=JE_WLY_Clinet\HotUpdateScripts\HotUpdateScripts.csproj"
set "OUTD=JE_WLY_Clinet\Assets\HotUpdateResources\Main\Dll\Hidden~"
set "OUTF=%OUTD%\HotUpdateScripts.dll"

echo ==========================================
echo   Build HotUpdateScripts.dll
echo ==========================================
echo.

if not exist "%MSB%" goto no_msb
if not exist "%PROJ%" goto no_proj

echo [INFO] Close Unity before building, or the dll may be locked.
echo.
echo [1/2] compiling ...
"%MSB%" "%PROJ%" /p:Configuration=Debug /v:minimal /nologo
if errorlevel 1 goto build_fail

echo.
echo [2/2] done.
echo.
if exist "%OUTF%" dir "%OUTF%"
echo.
echo [OK] Now open Unity and press Play.
echo.
if not defined NOPAUSE pause
exit /b 0

:no_msb
echo [ERROR] MSBuild not found:
echo         %MSB%
goto end_fail

:no_proj
echo [ERROR] csproj not found:
echo         %PROJ%
goto end_fail

:build_fail
echo.
echo [FAILED] compile error, dll NOT updated.
echo          Open JE_WLY_Clinet\HotUpdateScripts\HotUpdateScripts.sln
echo          in Visual Studio to see the details.
goto end_fail

:end_fail
echo.
if not defined NOPAUSE pause
exit /b 1
