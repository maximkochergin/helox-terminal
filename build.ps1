$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (!(Test-Path -LiteralPath $compiler)) { throw '.net framework 4.x compiler is required' }
$output = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$sources = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName
& $compiler /nologo /optimize+ /target:exe /platform:anycpu '/reference:System.Windows.Forms.dll' '/reference:System.Web.Extensions.dll' "/out:$output\helox.exe" @sources
if ($LASTEXITCODE -ne 0) { throw 'build failed' }
Write-Host 'built / bin\helox.exe'
