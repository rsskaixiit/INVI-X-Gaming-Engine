param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',
    [switch]$NoObfuscate
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root 'src\INVIX.App\INVIX.App.csproj'
$out = Join-Path $root "publish\$Runtime"
$binDir = Join-Path $root "src\INVIX.App\bin\Release\net8.0-windows\$Runtime"
$objDir = Join-Path $root "src\INVIX.App\obj\Release\net8.0-windows\$Runtime"
$obfOut = Join-Path $binDir '_obf'
$obfCfg = Join-Path $root "obj\obfuscar.$Runtime.xml"
$obfLog = Join-Path $root "obj\obfuscar.$Runtime.log"
$patcher = Join-Path $root 'tools\patch-integrity.ps1'
$tool = Join-Path $env:USERPROFILE '.dotnet\tools\obfuscar.console.exe'
$obfuscate = -not $NoObfuscate

if ($obfuscate) {
    Write-Host "[INVI X] Extra-hardened + obfuscated publish ($Runtime) ..." -ForegroundColor Magenta
}
else {
    Write-Host "[INVI X] Hardened publish ($Runtime, no obfuscation) ..." -ForegroundColor Magenta
}

Get-Process INVIX -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

if ($obfuscate -and -not (Test-Path $tool)) {
    Write-Host "[INVI X] Installing Obfuscar tool ..." -ForegroundColor DarkGray
    dotnet tool install --global Obfuscar.GlobalTool --version 2.2.50 | Out-Null
}

if ($obfuscate) {
    New-Item -ItemType Directory -Force -Path (Split-Path $obfCfg) | Out-Null
    $template = @'
<?xml version='1.0'?>
<Obfuscator>
  <Var name="InPath" value="{0}" />
  <Var name="OutPath" value="{1}" />
  <Var name="KeepPublicApi" value="false" />
  <Var name="HidePrivateApi" value="true" />
  <Var name="HideStrings" value="true" />
  <Var name="RenameProperties" value="true" />
  <Var name="RenameEvents" value="true" />
  <Var name="RenameFields" value="true" />
  <Var name="ReuseNames" value="true" />
  <Var name="UseUnicodeNames" value="true" />
  <Var name="RegenerateDebugInfo" value="false" />
  <Var name="OptimizeMethods" value="true" />
  <Var name="SuppressIldasm" value="false" />
  <Var name="LogFile" value="{2}" />
  <Module file="$(InPath)\INVIX.dll">
    <SkipType name="Invix.App" />
    <SkipType name="Invix.MainWindow" />
    <SkipNamespace name="Invix.ViewModels" />
    <SkipNamespace name="Invix.Models" />
  </Module>
</Obfuscator>
'@
    $xml = $template -f $binDir, $obfOut, $obfLog
    Set-Content -Path $obfCfg -Value $xml -Encoding UTF8
}

Remove-Item $out, $binDir, $objDir, $obfOut -Recurse -Force -ErrorAction SilentlyContinue

$publishArgs = @(
    'publish', $proj,
    '-c', 'Release',
    '-r', $Runtime,
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:PublishReadyToRun=false',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:IncludeAllContentForSelfExtract=true',
    '-p:DebugType=none',
    '-p:DebugSymbols=false',
    '-o', $out
)

if ($obfuscate) {
    $publishArgs += @(
        '-p:Obfuscate=true',
        "-p:ObfuscarTool=$tool",
        "-p:ObfuscarConfig=$obfCfg",
        "-p:ObfuscarOutDir=$obfOut",
        "-p:IntegrityPatcher=$patcher"
    )
}

dotnet @publishArgs

Remove-Item $obfOut -Recurse -Force -ErrorAction SilentlyContinue

$exe = Join-Path $out 'INVIX.exe'
if (-not (Test-Path $exe)) { throw "publish failed: $exe not found" }
$size = [math]::Round((Get-Item $exe).Length / 1MB, 2)
Write-Host "[INVI X] Done -> $exe  ($size MB)" -ForegroundColor Magenta
