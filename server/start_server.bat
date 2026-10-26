@echo off
setlocal
chcp 65001 >nul
title Machao Local Server

set "ROOT=%~dp0"
set "NS=%ROOT%nanoserver"
set "REDIS=E:\DevelopTools\Xoreax\IncrediBuild\redis-server.exe"
set "REDISCONF=%ROOT%..\tempbackup\redis_data\redis.conf"
set "LOGDIR=%ROOT%..\tempbackup"
set "LOGFILE=%LOGDIR%\server_run.log"

echo ============================================
echo  Machao single-player server launcher
echo ============================================
echo.

rem ---------- 0. MySQL check ----------
netstat -ano | findstr ":3306 " >nul
if errorlevel 1 (
    echo [X] MySQL 3306 is NOT listening. Start MySQL first.
    pause
    exit /b 1
)
echo [OK] MySQL 3306 is up.

rem ---------- 1. Redis ----------
netstat -ano | findstr ":6379 " >nul
if errorlevel 1 (
    echo [..] Starting Redis ...
    start "redis" /min "%REDIS%" "%REDISCONF%"
    timeout /t 2 >nul
    netstat -ano | findstr ":6379 " >nul
    if errorlevel 1 (
        echo [X] Redis failed to start.
        pause
        exit /b 1
    )
    echo [OK] Redis started.
) else (
    echo [OK] Redis already running.
)

rem ---------- 2. Game server ----------
echo [..] Starting game server (GOMEMLIMIT=2GiB) ...
echo      cwd : %NS%
echo      log : %LOGFILE%
echo.
rem LOGDIR must exist, otherwise the redirection fails and the whole line is skipped
if not exist "%LOGDIR%" mkdir "%LOGDIR%"
cd /d "%NS%"
set GOMEMLIMIT=2GiB
build\server.exe -c .\configs\config.toml > "%LOGFILE%" 2>&1

echo.
echo [X] Server exited. Check %LOGFILE%
pause
