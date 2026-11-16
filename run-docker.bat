@echo off
title Starting MYMO with Docker Compose + SQL Server
echo ================================================================
echo Starting MYMO Backend (5 Microservices + Gateway + SQL Server)
echo ================================================================
cd /d "%~dp0"
docker compose up -d
echo.
echo ================================================================
echo MYMO Docker Stack is RUNNING!
echo - Gateway: http://localhost:5079
echo - Centralized Swagger Hub: http://localhost:5079/swagger
echo - SQL Server Host Port: localhost:1433 (User: sa, Pass: YourStrong@Passw0rd)
echo - RabbitMQ Management: http://localhost:15672 (User: mymo)
echo ================================================================
pause
