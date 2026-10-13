param([string]$Root=$PSScriptRoot,[switch]$Quiet)
$ErrorActionPreference='Stop'
if ($PSVersionTable.PSEdition -eq 'Desktop') {
    $env:PSModulePath=(Join-Path $PSHOME 'Modules')+[IO.Path]::PathSeparator+$env:PSModulePath
}
$rootPath=[IO.Path]::GetFullPath($Root).TrimEnd('\')
$manifestPath=Join-Path $rootPath 'release-manifest.json'
if (!(Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'release manifest missing / extract the complete release archive' }
$actual=New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$pending=New-Object 'Collections.Generic.Stack[string]';$pending.Push($rootPath)
while ($pending.Count -gt 0) {
    $entry=Get-Item -LiteralPath $pending.Pop() -Force
    if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'linked package file / verification stopped' }
    if ($entry.PSIsContainer) { foreach ($child in Get-ChildItem -LiteralPath $entry.FullName -Force) { $pending.Push($child.FullName) } }
    elseif (![string]::Equals($entry.FullName,$manifestPath,[StringComparison]::OrdinalIgnoreCase)) { $null=$actual.Add($entry.FullName.Substring($rootPath.Length+1).Replace('\','/')) }
}
$manifest=[IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json
if ($manifest.FormatVersion -ne 1 -or $manifest.Version -notmatch '^\d+\.\d+\.\d+$' -or !$manifest.Files -or @($manifest.Files).Count -gt 10000) { throw 'invalid release manifest' }
$seen=New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($file in $manifest.Files) {
    $relative=[string]$file.Path
    if (!$relative -or $relative.Contains('\') -or $relative.StartsWith('/') -or $relative.Contains(':') -or @($relative.Split('/') | Where-Object { $_ -eq '' -or $_ -eq '.' -or $_ -eq '..' }).Count -gt 0 -or !$seen.Add($relative) -or $file.Sha256 -notmatch '^[0-9a-fA-F]{64}$') { throw 'invalid release manifest entry' }
    $path=[IO.Path]::GetFullPath((Join-Path $rootPath $relative))
    if (!$path.StartsWith($rootPath+'\',[StringComparison]::OrdinalIgnoreCase) -or !$actual.Contains($relative)) { throw ('release file missing / '+$relative) }
    $entry=Get-Item -LiteralPath $path -Force
    if ($entry.Length -ne $file.Bytes -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Sha256) { throw ('release file changed / '+$relative) }
}
if ($seen.Count -ne $actual.Count) { throw 'unexpected files in release folder / verify a fresh extraction' }
if (!$Quiet) { Write-Host ('verified / helox '+$manifest.Version+' / '+$seen.Count+' files match the release manifest') }
