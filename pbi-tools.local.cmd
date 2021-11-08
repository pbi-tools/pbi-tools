@ECHO OFF

PUSHD %~dp0

.\.build\dist\desktop\pbi-tools.exe %*

POPD