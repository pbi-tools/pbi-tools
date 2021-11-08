@ECHO OFF

PUSHD %~dp0

.\.build\dist\core\win-x64\pbi-tools.core.exe %*

POPD