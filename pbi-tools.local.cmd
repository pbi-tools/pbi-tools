@ECHO OFF

PUSHD %~dp0

.\.build\dist\pbi-tools.exe %*

POPD