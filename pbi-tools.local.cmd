@ECHO OFF

PUSHD %~dp0

.\.build\dist\full\pbi-tools.exe %*

POPD