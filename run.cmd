@echo off
if not exist "%~dp0build\Starfall.exe" call "%~dp0build.cmd"
if not exist "%~dp0build\Starfall.exe" exit /b 1
"%~dp0build\Starfall.exe" %*
