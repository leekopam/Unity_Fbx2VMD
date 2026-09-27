@echo off
setlocal

set "PROJECT_ROOT=%~dp0"
if "%PROJECT_ROOT:~-1%"=="\" set "PROJECT_ROOT=%PROJECT_ROOT:~0,-1%"

call "%PROJECT_ROOT%\Docs\Workflow\Tools\Local\StartWorkbench.bat" %*
exit /b %ERRORLEVEL%
