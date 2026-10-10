param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$Elevate
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $root "src\INVIX.App\bin\$Configuration\net8.0-windows\INVIX.exe"

if (-not (Test-Path $exe)) {
    throw "INVI X is not built yet. Run .\build.ps1 -Configuration $Configuration first."
}

if ($Elevate) {
    Write-Host "[INVI X] Launching elevated..." -ForegroundColor Magenta
    Start-Process -FilePath $exe -Verb RunAs
}
else {
    Write-Host "[INVI X] Launching..." -ForegroundColor Magenta
    & $exe
}
