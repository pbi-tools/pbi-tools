
pushd $PSScriptRoot\..\.build\dist\desktop

if ($env:PATH -notlike "*${pwd}*") {
    $env:PATH = "$pwd;$env:PATH"
    Write-Host "Added '$pwd' to PATH."
}

popd

pushd $PSScriptRoot\..\.build\dist\core\win-x64

if ($env:PATH -notlike "*${pwd}*") {
    $env:PATH = "$pwd;$env:PATH"
    Write-Host "Added '$pwd' to PATH."
}

popd