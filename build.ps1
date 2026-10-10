param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Push-Location $root
try {
    Write-Host "[INVI X] Restoring..." -ForegroundColor Magenta
    dotnet restore .\INVIX.sln

    Write-Host "[INVI X] Building ($Configuration)..." -ForegroundColor Magenta
    dotnet build .\INVIX.sln -c $Configuration --no-restore

    Write-Host "[INVI X] Output: $root\src\INVIX.App\bin\$Configuration\net8.0-windows\INVIX.exe" -ForegroundColor Cyan
}
finally {
    Pop-Location
}
