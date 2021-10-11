@ECHO OFF

PUSHD %~dp0

.\.build\dist\core\pbi-tools.core.exe %*

POPD