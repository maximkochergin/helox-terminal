# helox

mouse controls for windows, in a terminal.

**[download](https://github.com/maximkochergin/helox-terminal/releases/latest)** &nbsp; / &nbsp; [manual](docs/manual.md) &nbsp; / &nbsp; [releases](https://github.com/maximkochergin/helox-terminal/releases)

---

## start

extract the release zip, open `launch.bat`, choose a number.

```text
  1  acceleration     2  pointer speed
  3  test hz / gaps   4  check dpi
  5  profiles         6  more
  7  mouse status     8  aim tools
  0  exit
```

built for the trust gxt 929 helox. windows 10/11, .net framework 4.x.
opening the app leaves your settings unchanged. no account, telemetry or background app.

## controls

**adjust** — windows pointer speed, acceleration, scrolling and buttons, with readback after changes.

**measure** — delivered input frequency, interval tails and gaps. compare repeat tests for the same mouse. estimate dpi with three mouse-length passes.

**save** — named profiles, a `current -> saved` preview, undo for the last windows change and an original-settings backup.

**aim** — optional raw accel integration. choose the acceleration limit, stabilize its response, and adjust output smoothing separately. [setup](docs/manual.md#aim-tools).

**maintain** — check driver installation and live readback. remove the driver separately, or reset all helox data from `6 more`. [checks and cleanup](docs/manual.md#driver-checks-and-removal).

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
the full tests temporarily change windows preferences and restore them; `check` does not apply settings.

</details>

---

[hardware notes](docs/hardware.md) &nbsp; / &nbsp; [aim research](docs/aim.md) &nbsp; / &nbsp; [verification](docs/verification.md)

[mit licensed](LICENSE). use, modify and redistribute with the copyright and license notice retained. supplied without warranty. the optional raw accel package retains its own mit license.
