param([switch]$Quiet)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (!(Test-Path -LiteralPath $compiler)) { throw '.net framework 4.x compiler is required' }
$output = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$stage = Join-Path $output ('build-' + [guid]::NewGuid().ToString('n'))
New-Item -ItemType Directory -Path $stage | Out-Null
$compiled = Join-Path $stage 'helox.exe'
$destination = Join-Path $output 'helox.exe'
try {
    & $compiler /nologo /optimize+ /target:exe /platform:anycpu '/reference:System.Windows.Forms.dll' '/reference:System.Web.Extensions.dll' "/out:$compiled" @sources
    if ($LASTEXITCODE -ne 0) { throw 'build failed / previous executable kept' }
    try {
        try { [IO.File]::Move($compiled, $destination) }
        catch [IO.IOException] {
            if (!(Test-Path -LiteralPath $destination)) { throw }
            [IO.File]::Replace($compiled, $destination, [NullString]::Value)
        }
    } catch { throw 'could not replace executable / close helox and retry / previous executable kept' }
} finally {
    if (Test-Path -LiteralPath $compiled) { Remove-Item -LiteralPath $compiled -Force }
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage }
}
if (!$Quiet) { Write-Host 'built / bin\helox.exe' }
