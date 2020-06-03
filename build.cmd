@echo off
cls

ECHO:** DOTNET TOOL RESTORE
dotnet tool restore

ECHO:** DOTNET BUILD
if "%~1"=="" (dotnet fake build) else (dotnet fake run build.fsx -t %*)