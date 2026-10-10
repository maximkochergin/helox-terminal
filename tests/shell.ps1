$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$coreShell=Get-Command pwsh.exe -ErrorAction SilentlyContinue
if (!$coreShell) { Write-Host 'skipped / powershell 7 module inheritance / host unavailable';return }
$parentModules=& $coreShell.Source -NoProfile -Command '$env:PSModulePath'
if ($LASTEXITCODE -ne 0 -or !$parentModules) { throw 'could not read powershell 7 module paths' }
function Invoke-ShellProbe([string]$Prelude) {
    $commands='Get-Command Get-FileHash,Get-AuthenticodeSignature,Expand-Archive -ErrorAction Stop | Select-Object -ExpandProperty Name'
    $probeCommand='$ErrorActionPreference=''Stop''; '+$Prelude+$commands
    $encoded=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($probeCommand))
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $start.Arguments='-NoProfile -ExecutionPolicy Bypass -EncodedCommand '+$encoded
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $start.EnvironmentVariables['PSModulePath']=[string]$parentModules
    $process=[Diagnostics.Process]::Start($start)
    try {
        $output=$process.StandardOutput.ReadToEnd();$errorOutput=$process.StandardError.ReadToEnd();$process.WaitForExit()
        return @{Code=$process.ExitCode;Output=$output;Error=$errorOutput}
    } finally { $process.Dispose() }
}
$broken=Invoke-ShellProbe ''
if ($broken.Code -eq 0) { throw 'fixture did not reproduce module-path contamination' }
$maintenance=(Join-Path $project 'maintenance.ps1').Replace("'","''")
$repaired=Invoke-ShellProbe (". '"+$maintenance+"' -FunctionsOnly; ")
if ($repaired.Code -ne 0 -or $repaired.Output -notmatch 'Get-FileHash' -or $repaired.Output -notmatch 'Get-AuthenticodeSignature' -or $repaired.Output -notmatch 'Expand-Archive') {
    throw ('windows built-in module recovery failed / '+$repaired.Error)
}
Write-Host 'passed / inherited shell modules / hash, signature and archive commands'
