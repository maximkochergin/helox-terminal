![helox terminal — a white ribbon tracing a precise arc across a dark surface](docs/assets/helox-cover.png)

# helox terminal

a small windows terminal for the trust gxt 929 helox. mouse settings, input diagnostics and optional aim tools, launched from one batch file.

**[download](https://github.com/maximkochergin/helox-terminal/releases/latest)** · [quick start](#start) · [manual](docs/manual.md) · [source](src) · [license](LICENSE)

## start

1. download the release zip and extract it.
2. open `launch.bat`.
3. choose a number and press enter.

```text
  1  acceleration     2  pointer speed
  3  test hz / gaps   4  check dpi
  5  profiles         6  more
  7  mouse status     8  aim tools
  0  exit
```

empty answers, `0` or `back` cancel a prompt. enter or escape returns from a result screen. opening the terminal does not change settings.

windows 10/11 · .net framework 4.x · no account or background app

## the controls

| tool | what it does |
| :--- | :--- |
| **pointer** | sets windows speed, acceleration, scrolling and button preferences; checks the result after writing. |
| **input test** | measures delivered hz, p95/p99 intervals and gaps for the selected mouse. |
| **dpi check** | estimates dpi with three mouse-body-length passes, without a ruler. |
| **mouse status** | reads receiver identity, installed driver and hid descriptors; separates readings from model specifications. |
| **profiles** | saves windows settings and restores the original snapshot. |
| **aim tools** | applies a gradual acceleration curve or optional output smoothing through raw accel. |

current sensor dpi, configured hardware hz and battery are unknown. the terminal shows `?` or `null` for those fields. measured input hz and estimated dpi are labeled separately; receiver detection does not confirm mouse power.

## aim tools

open **`8 → 5 install driver`**, approve windows uac, close the installer with a keypress and restart windows once. then reopen the terminal and choose:

| control | effect |
| :--- | :--- |
| **`1 precision on`** | slow corrections stay at 1×; faster movement gradually approaches 1.4×. |
| **`3 smooth on`** | reduces movement magnitude fluctuations with a 4 ms half-life; preserves direction and adds input delay. |
| **`2 / 4`** | turns the corresponding feature off. |
| **`6 undo aim`** | restores the complete driver snapshot from before the first aim change. |

aim settings reset on reboot. enable them again when needed. basic controls need no administrator rights; aim installation needs 64-bit windows, .net 4.7.2+, the visual c++ x64 runtime and one restart. the installer checks the official package hash and driver signature.

windows acceleration in menu `1` is separate: raw input games bypass it. the aim driver transforms input before it reaches those games. presets are starting points; [read the mechanics and verification](docs/aim.md).

![motion study — two white physical ribbons on a black slab, one irregular and one sweeping](docs/assets/motion-study.png)

<sub>motion study / original artwork, not measurement data</sub>

## if movement feels uneven

run `3` while moving continuously. compare the intervals and gaps with the receiver near the mouse and away from active usb 3 devices or cables. pauses and wireless interruptions can both create long gaps. smoothing cannot recover missing reports.

dpi checks require an untransformed input stream. disable or restore aim filters first. the three-pass result is an estimate; repeatability does not establish absolute accuracy.

## documentation

[full manual](docs/manual.md) · [hardware findings](docs/hardware.md) · [aim research](docs/aim.md) · [verification](docs/verification.md)

<details>
<summary>commands and json output</summary>

```text
launch.bat status --json
launch.bat measure 10 --json
launch.bat aim status --json
launch.bat aim precision on
launch.bat aim smooth off
launch.bat aim restore
```

`help` lists all commands. exit code `0` means success; `1` means failure. profiles and backups live in `%localappdata%\helox-terminal`. exported diagnostics contain device paths; review them before sharing.

</details>

<details>
<summary>build from source</summary>

```powershell
powershell.exe -noprofile -executionpolicy bypass -file .\build.ps1
powershell.exe -noprofile -executionpolicy bypass -file .\tests\run.ps1
```

the batch launcher builds automatically if the executable is missing. rebuild after changing source. tests temporarily change windows preferences and restore them. official aim engine tests run after `aim prepare`; physical game response requires a manual check.

</details>

## license

[mit](LICENSE). use, modify and redistribute, including commercially, with the copyright and license notice retained. supplied as is, without warranty.

the optional raw accel backend has its own mit license, retained in the downloaded package. the interface, presets and integration are maintained here.
