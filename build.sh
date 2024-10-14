#!/bin/bash
clear

echo "** DOTNET TOOL RESTORE"
dotnet tool restore

echo "** DOTNET BUILD"
dotnet run --project ./build -- -t "$@"