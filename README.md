![helox — mouse controls, in a terminal](docs/assets/helox.svg)

**[download](https://github.com/maximkochergin/helox-terminal/releases/latest)** &nbsp; / &nbsp; [manual](docs/manual.md) &nbsp; / &nbsp; [releases](https://github.com/maximkochergin/helox-terminal/releases)

---

## start

extract the release zip, open `launch.bat`, choose a number.

![helox home menu: acceleration, pointer speed, test hz and gaps, check dpi, profiles, more, mouse status, aim tools, game presets and exit](docs/assets/terminal.svg)

menu illustration / choose `9 game presets` for a complete setup.

built for the trust gxt 929 helox. windows 10/11, .net framework 4.x.
opening the app leaves your settings unchanged. no account, telemetry or background app.

## controls

**adjust** — windows pointer speed, acceleration, scrolling and buttons, with readback after changes.

**measure** — delivered input frequency, interval tails and gaps. compare repeat tests for the same mouse. estimate dpi with three mouse-length passes.

**save** — named profiles, a `current -> saved` preview, undo for the last windows change and an original-settings backup.

**aim** — optional raw accel integration. build a personal curve and preview its response before applying. independent stability, smoothing, snapping, direction scales and micro damping; one selected-mouse bypass. [curves](docs/curves.md) / [filters](docs/filters.md) / [setup](docs/manual.md#aim-tools).

**game presets** — complete input recipes for valorant, cs2 and matched kovaak's training. preview, apply and undo windows + driver settings together. sensitivity and fov steps stay explicit. [recipes](docs/game-presets.md).

start with `9 game presets`: choose your game, review what turns on or off, then apply. the screen checks which recipe matches the live driver. `preset status` checks again after edits or a restart; saved choices alone do not mean a preset is active.

flicks leave a tail? `8 aim tools > 22 remove flick tail` disables output averaging while keeping your curve. it avoids this filter's overshoot; it cannot reconstruct a sensor spinout. [movement checks](docs/movement.md).

an interrupted preset keeps a recovery snapshot. reopen `9 > 6` to restore it, or run `preset recover` with games closed.

**maintain** — check installation, live readback and the selected mouse's driver stack. remove the driver separately, or reset all helox data from `6 more`. [checks and cleanup](docs/manual.md#driver-checks-and-removal).

## readings

hardware dpi, configured polling rate and battery remain unknown. measured hz and estimated dpi are separate readings. smoothing adds delay and cannot recover missing reports.

windows acceleration is separate from the aim driver: raw input games bypass the windows setting. aim installation needs administrator rights and a restart; saved aim choices can be resumed after reboot.

<details>
<summary>commands</summary>

```text
launch.bat status --json
launch.bat measure 10 --json
launch.bat profile show training
launch.bat undo
launch.bat aim resume
launch.bat aim response
launch.bat preset show valorant
launch.bat preset preview cs2
launch.bat aim verify
launch.bat aim curve
launch.bat aim events
launch.bat check
```

`help` lists every command. `profile show training` requires a saved profile named `training`.
settings and backups stay in `%localappdata%\helox-terminal`.

</details>

<details>
<summary>build and test</summary>

```powershell
powershell.exe -noprofile -executionpolicy bypass -file .\build.ps1
powershell.exe -noprofile -executionpolicy bypass -file .\tests\run.ps1
```

the launcher builds if the executable is missing. rebuild after source changes.
the full tests temporarily change windows preferences and restore them; `check` does not apply settings. `tests\live.ps1` separately exercises real driver writes, all five recipe apply/undo paths and persistence-failure recovery with games closed.

</details>

---

[hardware notes](docs/hardware.md) &nbsp; / &nbsp; [aim research](docs/aim.md) &nbsp; / &nbsp; [verification](docs/verification.md)

[mit licensed](LICENSE). use, modify and redistribute with the copyright and license notice retained. supplied without warranty. the optional raw accel package retains its own mit license.
