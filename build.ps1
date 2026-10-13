param([switch]$Quiet)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Desktop') {
    $env:PSModulePath=(Join-Path $PSHOME 'Modules')+[IO.Path]::PathSeparator+$env:PSModulePath
}
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (!(Test-Path -LiteralPath $compiler)) { throw '.net framework 4.x compiler is required' }
$output = Join-Path $PSScriptRoot 'bin'
$identity = [Text.Encoding]::UTF8.GetBytes([IO.Path]::GetFullPath($PSScriptRoot).ToLowerInvariant())
$sha = [Security.Cryptography.SHA256]::Create()
try { $buildKey = [BitConverter]::ToString($sha.ComputeHash($identity)).Replace('-','') } finally { $sha.Dispose() }
$gate = New-Object Threading.Mutex($false, ('Local\helox-build-' + $buildKey))
$held = $false
$stage = Join-Path $output ('build-' + [guid]::NewGuid().ToString('n'))
$compiled = Join-Path $stage 'helox.exe'
$installed = @()
$keepStage = $false
try {
    try { $held = $gate.WaitOne(0) } catch [Threading.AbandonedMutexException] { $held = $true }
    if (!$held) { throw 'another build is running / wait and retry' }
    if ((Test-Path -LiteralPath $output) -and ((Get-Item -LiteralPath $output -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'linked build output / build stopped' }
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File | Sort-Object Name | ForEach-Object FullName)
    if ($sources.Count -eq 0) { throw 'source files missing / extract the complete release archive' }
    New-Item -ItemType Directory -Path $stage | Out-Null
    $references = @('/reference:System.Windows.Forms.dll','/reference:System.Web.Extensions.dll')
    & $compiler /nologo /optimize+ /target:exe /platform:anycpu /main:Helox.Program @references "/out:$compiled" @sources
    if ($LASTEXITCODE -ne 0) { throw 'build failed / previous executable kept' }
    $startup = Join-Path $stage 'helox-startup.exe'
    & $compiler /nologo /optimize+ /target:winexe /platform:anycpu /main:Helox.StartupEntry @references "/out:$startup" @sources
    if ($LASTEXITCODE -ne 0) { throw 'startup helper build failed / previous executables kept' }
    try {
        # Install the hidden helper first. If the console app is open, roll the
        # helper back so a failed build does not leave mixed application versions.
        foreach ($name in @('helox-startup.exe','helox.exe')) {
            $candidate = Join-Path $stage $name
            $destination = Join-Path $output $name
            $backup = Join-Path $stage ($name + '.previous')
            $existed = Test-Path -LiteralPath $destination
            if ($existed) { [IO.File]::Replace($candidate,$destination,$backup) }
            else { [IO.File]::Move($candidate,$destination) }
            $installed += @{Destination=$destination;Backup=$backup;Existed=$existed}
        }
    } catch {
        try {
            for ($i=$installed.Count-1;$i -ge 0;$i--) {
                $item=$installed[$i]
                if ($item.Existed) { [IO.File]::Replace($item.Backup,$item.Destination,[NullString]::Value) }
                else { Remove-Item -LiteralPath $item.Destination -Force }
            }
        } catch {
            $keepStage=$true
            throw ('build rollback blocked / close helox / previous executables retained in '+$stage)
        }
        throw 'could not replace executable / close helox and retry / previous executables kept'
    }
} finally {
    try {
        if (!$keepStage -and (Test-Path -LiteralPath $stage)) {
            foreach ($file in Get-ChildItem -LiteralPath $stage -File) { Remove-Item -LiteralPath $file.FullName -Force }
            Remove-Item -LiteralPath $stage
        }
    } finally {
        if ($held) { $gate.ReleaseMutex() }
        $gate.Dispose()
    }
}
if (!$Quiet) { Write-Host 'built / bin\helox.exe + bin\helox-startup.exe' }
