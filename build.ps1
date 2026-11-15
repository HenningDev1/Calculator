# Builds publish\Calculator.exe (portable app) and publish\CalculatorSetup.exe (installer).
# Both are the same self-contained single file: the PC needs no .NET installed.
# Run the setup exe to install; it also accepts --install for a silent install.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$out = Join-Path $PSScriptRoot 'publish'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish Calculator.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none -o $out
if ($LASTEXITCODE) { throw 'Publish failed' }

Copy-Item (Join-Path $out 'Calculator.exe') (Join-Path $out 'CalculatorSetup.exe')
Get-ChildItem $out -Filter *.exe | Select-Object Name, @{n='MB';e={[math]::Round($_.Length/1MB,1)}}
