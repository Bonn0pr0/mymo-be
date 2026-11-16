@echo off
title Stopping MYMO Docker Stack
echo ================================================================
echo Stopping MYMO Backend containers...
echo ================================================================
cd /d "%~dp0"
docker compose down
echo.
echo All containers have been stopped.
pause
