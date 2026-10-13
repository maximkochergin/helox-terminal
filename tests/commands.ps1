# Hosted-safe command contracts: no setting writes, registry writes or driver installation.
$ErrorActionPreference='Stop'
$executable=Join-Path (Split-Path $PSScriptRoot -Parent) 'bin\helox.exe'
function Invoke-Json([string[]]$Arguments,[int]$Code=0) {
    $output=& $executable @Arguments --json
    if ($LASTEXITCODE -ne $Code) { throw ('command exit code / '+($Arguments -join ' ')+' / '+($output -join "`n")) }
    return ($output | ConvertFrom-Json)
}
$before=Invoke-Json -Arguments @('status')
$version=Invoke-Json -Arguments @('version')
if ($version.version -ne '0.20.0' -or $before.version -ne $version.version -or !$before.recovery.PSObject.Properties['windows'] -or !$before.startup.Note) { throw 'version, recovery or startup status contract failed' }
$startup=Invoke-Json -Arguments @('startup','status')
if (!$startup.PSObject.Properties['Enabled'] -or !$startup.PSObject.Properties['RegistrationMatches']) { throw 'startup status fields missing' }
$health=Invoke-Json -Arguments @('health')
if ($health.Version -ne $version.version -or !$health.Startup.Note -or $null -ne $health.GameInputVerified) { throw 'health startup evidence contract failed' }
foreach ($arguments in @(@('startup'),@('startup','invalid'),@('startup','on','extra'),@('startup','off','extra'),@('recover','extra'),@('version','extra'),@('status','--json'),@('preset','apply','valorant','smooth=NaN'),@('set','speed','0'))) {
    $errorRecord=Invoke-Json -Arguments $arguments -Code 1
    if (!$errorRecord.error) { throw 'invalid command must return JSON error before mutations' }
}
$after=Invoke-Json -Arguments @('status')
if (($before.windows | ConvertTo-Json -Compress) -ne ($after.windows | ConvertTo-Json -Compress) -or ($before.gameAcceleration | ConvertTo-Json -Compress) -ne ($after.gameAcceleration | ConvertTo-Json -Compress)) { throw 'read-only command checks changed settings' }
Write-Host 'passed / read-only version, startup, recovery, health and invalid command contracts'
$global:LASTEXITCODE=0
