@echo off
cls

ECHO:** DOTNET TOOL RESTORE
dotnet tool restore

ECHO:** DOTNET BUILD
dotnet run --project ./build/build.fsproj -- -t %*