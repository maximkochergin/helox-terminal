$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('helox launch fixture (x) '+[guid]::NewGuid().ToString('n'))
function Invoke-Launch([string]$Arguments) {
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=Join-Path $env:WINDIR 'System32\cmd.exe'
    $start.Arguments='/d /c ""'+(Join-Path $fixture 'launch.bat')+'" '+$Arguments+'"'
    $start.WorkingDirectory=[IO.Path]::GetTempPath()
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $process=[Diagnostics.Process]::Start($start)
    try { $output=$process.StandardOutput.ReadToEnd();$errors=$process.StandardError.ReadToEnd();$process.WaitForExit();return @{Code=$process.ExitCode;Output=$output;Errors=$errors} }
    finally { $process.Dispose() }
}
try {
    New-Item -ItemType Directory -Path (Join-Path $fixture 'src') -Force | Out-Null
    foreach ($name in @('launch.bat','build.ps1')) { Copy-Item -LiteralPath (Join-Path $project $name) -Destination (Join-Path $fixture $name) }
    [IO.File]::WriteAllText((Join-Path $fixture 'src\Program.cs'),'namespace Helox { class Program { static int Main(string[] args) { System.Console.WriteLine(System.String.Join("|",args)); return args.Length>1 ? 7 : 0; } } class StartupEntry { static void Main() {} } }')
    $first=Invoke-Launch '"value with space"'
    if ($first.Code -ne 0 -or $first.Output.Trim() -ne 'value with space' -or !(Test-Path -LiteralPath (Join-Path $fixture 'bin\helox-startup.exe'))) { throw ('launch rebuild or path quoting failed / '+$first.Errors) }
    $failed=Invoke-Launch '"value with space" fail'
    if ($failed.Code -ne 7 -or $failed.Output.Trim() -ne 'value with space|fail') { throw 'launch argument forwarding or exit code failed' }
    Write-Host 'passed / launch from another working directory, spaced paths, automatic build, argument forwarding and exit code'
} finally {
    $resolved=[IO.Path]::GetFullPath($fixture);$allowed=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if (!$resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'invalid fixture cleanup path' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
$global:LASTEXITCODE=0
