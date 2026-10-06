# helox terminal

minimal white-text windows cli for the trust gxt 929 helox. lowercase controls, numbered menu, batch launcher.

## start

download the zip from [releases](https://github.com/maximkochergin/helox-terminal/releases), extract it and run `launch.bat`.

```text
  helox / 0.3.0
  ----------------------------------------
  receiver  gxt 929 helox
  windows   speed 10/20 / accel on
  hardware  dpi ? / hz ?

  1  acceleration     2  pointer speed
  3  test hz          4  check dpi
  5  profiles         6  more
  7  mouse status     0  exit
```

type a number and press enter. empty answers, `0` or `back` cancel a prompt. result screens return to the menu with enter or escape. launching alone never changes settings.

**acceleration here is windows acceleration. raw input games bypass it.** helox does not install a game-wide acceleration driver. `setup` now enables windows acceleration and sets pointer speed to 10/20; an already active acceleration mode and its thresholds are preserved.

## what works

- actual receiver detection, driver/revision/descriptor dossier and live windows setting readback.
- windows pointer speed, acceleration, scrolling, double-click interval and primary button swap.
- profiles and original-setting restore; writes are checked and rolled back on failure.
- observed input frequency for one selected mouse, and distance-based dpi estimates.

hardware dpi/polling writes, current sensor dpi, configured polling, battery and rgb remain unavailable. `?` means unknown. use the physical dpi button. receiver presence does not confirm mouse power or wireless link.

## tests and recovery

`3` captures 10 seconds of continuous mouse movement. reported hz uses average active report delivery, which avoids enormous median-based values caused by queued batches. uneven delivery is flagged. this is an input measurement, not configured usb polling or click latency.

`4` checks approximate dpi without a ruler or pad marks. keep a fingertip beside the front edge of the mouse, then slide the mouse forward until the rear edge reaches that same stationary fingertip. this gives one mouse-body length of travel. press enter to start and finish each pass. do not rotate, lift or return during a pass. repeat three times; the app uses their median and rejects a spread above 15 percent. the gxt 929 body length is 125 mm according to the manufacturer. repeatability is not absolute accuracy: hand alignment and actual travel still matter.

this is an estimate of delivered raw counts per inch, not a sensor setting read. an external input filter can change those counts. exact automatic dpi cannot be derived from counts alone when the device provides no physical scale or readable setting. the connected mouse's x/y hid descriptors have no distance units or physical range. known-distance calibration remains available as `calibrate <cm>`; comma and dot decimal separators work.

`7` shows the detailed dossier. receiver identity, driver metadata and hid values are read locally; model dimensions/range/stage count come from [trust's product specifications](https://www.trust.com/en/product/25307-gxt-929-helox-ultra-lightweight-wireless-gaming-mouse). driver-declared button count and sample rate are labeled separately from physical controls and measured frequency. stored test results include timestamps in detailed status; unknown battery, current dpi, configured hz and mouse power stay unknown.

`5` saves/loads windows profiles or restores the original snapshot. data stays in `%localappdata%\helox-terminal`. history is labeled as history, never current hardware values. there is no telemetry.

saved profiles are selected by number when loading.

profiles must contain a complete, valid settings snapshot. a damaged original backup blocks new changes so recovery is not silently lost. simultaneous cli instances serialize settings changes and read current values after acquiring the lock.

## advanced commands

```text
setup
set acceleration on|off
set speed 1..20
set wheel 0..100|page
set doubleclick 200..900
set swap on|off
measure 3..30
dpi
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

tests temporarily change native settings and always restore them. regressions cover acceleration preservation, queued input timing, calibration timeout, three-pass dpi analysis, descriptor/device scoping, menu navigation, profile validation and input recovery. physical mouse-body/ruler measurements still need manual verification.

[hardware findings](docs/hardware.md) / [verification](docs/verification.md) / [windows raw input](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawmouse)

## license

released under the [mit license](LICENSE). you may use, modify and distribute this software, including for commercial projects, provided you retain the copyright notice and license text. the software is provided as is, without warranty. see [LICENSE](LICENSE) for the full terms.
