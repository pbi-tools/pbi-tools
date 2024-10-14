#!/bin/bash
clear

echo "** DOTNET TOOL RESTORE"
dotnet tool restore

echo "** DOTNET BUILD"
dotnet run --project ./src/PBI-Tools.NETCore/ -c Release -r linux-x64 -- "$@"