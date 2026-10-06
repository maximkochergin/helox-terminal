# helox terminal

minimal white-text windows cli for the trust gxt 929 helox. lowercase controls, numbered menu, batch launcher.

## start

download the zip from [releases](https://github.com/maximkochergin/helox-terminal/releases), extract it and run `launch.bat`.

```text
  helox / 0.2.0
  ----------------------------------------
  receiver  gxt 929 helox
  windows   speed 10/20 / accel on
  hardware  dpi ? / hz ?

  1  acceleration     2  pointer speed
  3  test hz          4  estimate dpi
  5  profiles         6  more
  0  exit
```

type a number and press enter. empty answers cancel a prompt. `home` returns to the menu. launching alone never changes settings.

**acceleration here is windows acceleration. raw input games bypass it.** helox does not install a game-wide acceleration driver. `setup` now enables windows acceleration and sets pointer speed to 10/20; an already active acceleration mode and its thresholds are preserved.

## what works

- actual receiver detection and live windows setting readback.
- windows pointer speed, acceleration, scrolling, double-click interval and primary button swap.
- profiles and original-setting restore; writes are checked and rolled back on failure.
- observed input frequency for one selected mouse, and distance-based dpi estimates.

hardware dpi/polling writes, current sensor dpi, configured polling, battery and rgb remain unavailable. `?` means unknown. use the physical dpi button. receiver presence does not confirm mouse power or wireless link.

## tests and recovery

`3` captures 10 seconds of continuous mouse movement. reported hz uses average active report delivery, which avoids enormous median-based values caused by queued batches. uneven delivery is flagged. this is an input measurement, not configured usb polling or click latency.

`4` asks for a measured distance in cm. mark that distance on the pad, place the mouse at the first mark, press enter, move once straight to the second mark, then press enter. do not lift or return. comma and dot decimal separators work. unfinished captures time out without saving. repeat to compare estimates.

`5` saves/loads windows profiles or restores the original snapshot. data stays in `%localappdata%\helox-terminal`. history is labeled as history, never current hardware values. there is no telemetry.

## advanced commands

```text
setup
set acceleration on|off
set speed 1..20
set wheel 0..100|page
set doubleclick 200..900
set swap on|off
measure 3..30
calibrate <cm>
profile save|apply <name>
profile list
restore
devices
select <index>
probe
status
faq
home
```

command mode: `launch.bat status --json`, `launch.bat probe --json`, `launch.bat measure 10 --json`. exit code 0 means success; 1 means failure. unsupported hardware values are null. diagnostics include machine-specific device paths; review before sharing.

## build

windows 10/11 with .net framework 4.x. no administrator account, python, npm, service or startup task required. from source, `launch.bat` compiles on first use. after source edits or pulling updates, rebuild with `build.ps1`.

```powershell
powershell.exe -noprofile -executionpolicy bypass -file .\build.ps1
powershell.exe -noprofile -executionpolicy bypass -file .\tests\run.ps1
```

tests temporarily change native settings and always restore them. regressions cover acceleration preservation, queued input timing, calibration timeout, decimal parsing, menu navigation and input recovery. physical motion/ruler measurements still need manual verification.

[hardware findings](docs/hardware.md) / [verification](docs/verification.md) / [windows raw input](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawmouse)

inspired by [wallhack terminal](https://terminal.wallhack.com/); independent of trust and wallhack.

## license

mit.
