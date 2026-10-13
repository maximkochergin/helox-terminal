# operation and recovery / 0.20.0

## a stable everyday setup

1. extract the complete release into a permanent folder and open `launch.bat`.
2. choose the intended mouse in `6 more > 1 choose mouse`. this menu selection is remembered by raw-input path; disconnected mice are reported instead of silently replaced. command-mode `select` and `--mouse` remain temporary.
3. install the optional official raw accel backend through `2 tune mouse > 3 driver > 1 install driver`, then restart once. basic windows controls and input measurements work without it.
4. use `1 game setup` or individual aim tools. review the response comparison and apply once. `aim status` reads the live configuration; `preset status` compares it with saved and built-in recipes.
5. optionally enable `startup on` through the command line or `2 tune mouse > 3 driver > 8 automatic sign-in resume`. `startup status` reports registration and the last helper result.
6. close the terminal. the active driver performs processing; the terminal need not remain running.

keep the physical dpi stage and in-game sensitivity consistent. windows pointer settings affect the desktop; game sensitivity and fov remain manual. personal movement captures expire after seven days for building a new personalized recipe. the complete already-saved aim controls do not require a fresh movement capture for `aim resume`.

## optional sign-in restoration

```text
launch.bat startup on
launch.bat startup status --json
launch.bat startup run --json
launch.bat startup off
```

registration is opt-in and per Windows user. `startup on` writes the `HeloxTerminal` value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, containing the quoted absolute path to `bin\helox-startup.exe`. the helper is a hidden Windows executable and runs once at sign-in; Windows may delay or disable startup applications. normal application launch does not apply saved controls. the helper is not a resident service, continuous game watcher or automatic per-game switcher. it restores saved aim controls, without applying windows pointer settings or editing game files.

enabling requires the selected mouse's live controls to match its saved controls and be enabled; a bypassed or changed setup must be explicitly resumed or reapplied first. `%localappdata%\helox-terminal\startup.json` stores the exact mouse path/id, helper path and the device calibration/timing captured at registration. the helper reads the latest saved controls and retains that captured calibration. its last result is written to `startup-last.json`; `startup status` reads this report without running restoration. `startup run` executes the same safe policy manually, rather than overriding a custom live setup.

automatic restoration is conservative: it checks the saved mouse identity, verified backend, available driver, supported-game process state and pending recovery. an already-matching selected setup needs no activation. otherwise automatic activation requires the complete global profile list, device list and default configuration to match canonical factory defaults. any custom/global/peer setup or live bypass is preserved. a disconnected mouse, unavailable process query, unavailable backend or recovery checkpoint leaves a reported skipped/failed result. game detection checks known Valorant, CS2 and Kovaak's process names only; unknown games and a game starting after the query are not verified. the helper does not install the driver, suppress anticheat errors or recover an interrupted transaction automatically. manual `aim resume` is the explicit way to replace a live setup with saved choices.

`startup off` removes automatic restoration; it does not undo the active driver configuration. after moving or replacing the application folder, changing the mouse's USB path/port, or deliberately changing software calibration/timing, run `startup on` from the intended copy to refresh the registration. review `startup status` after the first sign-in and use manual resume if automatic restoration was skipped. mouse and driver state can change later: the helper has no retry loop or ongoing monitor.

bypass is live driver state and is not stored in saved aim controls. `aim bypass on` alone makes the helper retain that live bypass, but reboot resets the driver. if automatic restoration remains enabled, the next cold driver restoration enables the saved profile again. to keep effects disabled across sign-ins, run `startup off` and then `aim bypass on`. resume or an effect-on action explicitly enables processing again.

## interrupted writes

the tool saves validated checkpoints before mutating the applicable settings, verifies readback and retains recovery evidence when rollback fails or the process exits. use the checkpoint indicated by `status` or `health`; close supported games before driver recovery.

| pending checkpoint | restore command | restores |
| --- | --- | --- |
| `windows-recovery.json` | `recover` | desktop settings and previous windows undo |
| `game-recovery.json` | `preset recover` | complete native/windows transaction state and exact saved-file snapshots |
| `verify-recovery.json` | `aim verify restore` | pre-verification complete driver state |

desktop recovery is also available in `5 saved profiles > 6 recover interrupted change`; aim/preset recovery is in `1 game setup > 5 undo or recover`. ordinary mutations stop while applicable recovery remains pending. recovery checks every required component and only clears its checkpoint after success. damaged recovery files are reported and retained rather than guessed or discarded.

these checkpoints support process-interruption recovery. atomic file replacement does not guarantee durability after power loss, disk damage, access changes or external writes. keep the data directory and error text when recovery fails. manually deleting checkpoints or backups can remove the ability to restore the pre-change state.

## routine diagnostics

`health` reads windows settings, backend integrity, the selected device's stack, saved/live recipe matching, recovery, startup and Vanguard service status. its successful report exit code does not mean that every subsystem is healthy. unknown evidence remains unknown. `check` runs pure regressions without applying native settings. `measure last` reads the selected mouse's last delivery capture.

`aim response` is an official-engine calculation with synthetic motion, separate from delivered input timing. `measure 10` needs continuous physical movement. gaps cannot distinguish a pause from radio interruption; neither command certifies game input or repairs a sensor spinout. see [movement checks](movement.md) and [verification](verification.md).

before sharing diagnostics, review machine-specific mouse paths, timestamps and local configuration. helox stores data in `%localappdata%\helox-terminal` and adds no telemetry or account.

## removal

disable sign-in restoration with `startup off` before retiring or moving the application. `aim restore` restores the complete original driver snapshot; windows `restore` restores the original desktop snapshot. those snapshots can include other devices because the upstream driver has one shared configuration.

`aim uninstall` disables sign-in restoration before removing the shared raw accel driver. if later removal fails, startup remains disabled; data is kept. `cleanup --confirm` restores available originals, removes the driver, disables sign-in restoration and deletes helox data after successful preflight. it waits for the terminal to close and refuses an active hidden helper or another helox session. failure to disable startup stops data deletion. close other helox sessions and games first. keep backups until restoration succeeds. a restart may still be needed to unload a removed driver. [full removal behavior](manual.md#driver-checks-and-removal).

## release and maintenance policy

0.20.0 closes planned feature development. the release keeps the signed raw accel 1.7.1 backend pinned to its verified archive and does not introduce a custom kernel driver, hardware protocol guesses or new package-manager dependencies. no further feature work is scheduled. windows, driver-signing policies, anticheat and game updates remain external dependencies; confirmed compatibility or security issues may require a future fix.

the repository remains available under MIT. existing releases and history remain accessible. the release itself does not change GitHub archive state or promise indefinite support.

when preparing a maintenance release, preserve the existing lowercase imperative commit style and `v<version> / <description>` release title. rebuild, run the checks appropriate to the change, package from the exact committed source and publish the resulting zip plus its SHA256. distinguish hosted regressions, local interactive windows tests, live driver tests and physical/game checks in the release notes. do not carry an earlier release's test result forward as new evidence.

## release package

the zip contains the `helox-terminal` folder with console and hidden-startup executables, launcher, complete source/tests/docs, build/package/verification/installation/maintenance scripts and license. `release-manifest.json` records the application version and every included file's relative path, size and SHA256, excluding the manifest itself. the archive sidecar is `helox-terminal-v0.20.0.zip.sha256` and contains the lowercase archive hash followed by its filename.

```powershell
Get-FileHash -LiteralPath .\helox-terminal-v0.20.0.zip -Algorithm SHA256
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\helox-terminal\verify-package.ps1
```

compare the archive hash with the value published on the GitHub release before extracting. the manifest checks extraction integrity; it does not authenticate its own origin or replace a digital signature. verification refuses missing, changed, extra or linked files and invalid/traversing manifest paths. run it on a fresh untouched extraction: rebuilding, editing documentation or adding files can legitimately change the checked tree.

`package.ps1` builds both binaries, compares staged `version --json` with the source version, verifies the staged file tree, creates the zip/checksum pair and cleans staging. publication failure attempts to restore the previous archive/checksum pair. a folder-specific packaging mutex rejects concurrent package publications. `build.ps1` compiles both outputs before installation and attempts to roll back the helper if the console output cannot be replaced; concurrent builds of the same folder are rejected by a separate build mutex. if another file lock also prevents rollback, staging backups remain available and the error reports their path.

backend preparation keeps the official archive pinned and verifies extracted files before reuse. it checks destination path chains for junctions/symlinks before repair and preserves the upstream settings file. a matching locked file can be reused; a changed locked file stops repair and asks you to close the application before retrying. earlier files may already have been repaired, but failed preparation does not proceed to driver installation/removal. download requests have a bounded timeout, and a new verified archive replaces the old cache atomically. these preparation checks are separate from installing or removing the live kernel driver.
