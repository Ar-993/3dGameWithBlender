@echo off
cd /d "%~dp0"
dotnet tool restore
if errorlevel 1 pause & exit /b 1
dotnet mgcb-editor "Content\Content.mgcb"
