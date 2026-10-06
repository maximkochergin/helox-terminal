$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$dist = Join-Path $PSScriptRoot 'dist'
$stage = Join-Path (Join-Path $dist ([guid]::NewGuid().ToString('n'))) 'helox-terminal'
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'bin') | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\helox.exe') -Destination (Join-Path $stage 'bin\helox.exe')
foreach ($name in @('launch.bat', 'README.md', 'LICENSE', 'build.ps1', 'install-aim.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $stage $name)
}
foreach ($folder in @('src', 'tests', 'docs')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $folder) -Destination $stage -Recurse -Force
}
$versionMatch = [regex]::Match((Get-Content -LiteralPath (Join-Path $PSScriptRoot 'src\Program.cs') -Raw), 'const string Version="([0-9]+\.[0-9]+\.[0-9]+)"')
if (!$versionMatch.Success) { throw 'application version missing' }
$archive = Join-Path $dist ('helox-terminal-v' + $versionMatch.Groups[1].Value + '.zip')
Compress-Archive -Path $stage -DestinationPath $archive -Force
Get-FileHash -LiteralPath $archive -Algorithm SHA256
