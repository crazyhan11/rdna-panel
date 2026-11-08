param(
    [string]$Adlx = $env:ADLX_SDK,
    [string]$Gxx  = 'g++',
    [string]$Out  = (Join-Path $PSScriptRoot '..\rt_core.dll')
)

if (-not $Adlx) { throw "ADLX SDK path required: -Adlx <path> or set ADLX_SDK" }
$include = Join-Path $Adlx 'SDK\Include'
$helper  = Join-Path $Adlx 'SDK\ADLXHelper\Windows\Cpp'
$winapis = Join-Path $Adlx 'SDK\Platform\Windows\WinAPIs.cpp'

$gxxCmd = Get-Command $Gxx -ErrorAction SilentlyContinue
if (-not $gxxCmd) { throw "g++ not found: $Gxx" }
if (-not (Test-Path $include)) { throw "ADLX SDK not found: $Adlx" }

& $gxxCmd.Source -std=c++17 -O2 -w -shared -static -o $Out `
    (Join-Path $PSScriptRoot 'rt_core.cpp') `
    (Join-Path $helper 'ADLXHelper.cpp') `
    $winapis `
    -I $include -I $helper -ladvapi32

if ($LASTEXITCODE -ne 0) { throw "build failed: $LASTEXITCODE" }
Write-Host "built: $Out"
