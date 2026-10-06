# helox terminal

minimal windows cli for the trust gxt 929 helox. white text, lowercase controls, launch from a batch file. inspired by the direct controls and sections of [wallhack terminal](https://terminal.wallhack.com/). independent of trust and wallhack.

**working windows settings and device diagnostics. hardware dpi/polling control is not implemented.** this is a user-mode utility using the installed windows hid driver, not a replacement kernel driver.

## start

download the portable zip from [releases](https://github.com/maximkochergin/helox-terminal/releases), extract it, and double-click `launch.bat`. windows 10/11 with .net framework 4.x; no administrator account, python, npm, installer, service, or startup task required.

from source, double-click `launch.bat`: it compiles the small executable with the windows .net framework compiler on first launch. subsequent launches execute the cached binary directly. run `build.ps1` again after editing source files.

```text
helox / status
helox / setup
helox / set speed 8
helox / measure 10
helox / calibrate 10
helox / profile save daily
helox / restore
```

launching the app only reads settings. `setup` saves an original snapshot, sets windows pointer speed to 10/20, disables windows pointer acceleration, and reads the settings back to verify them. it preserves scrolling, double-click timing, thresholds and button order. windows settings apply to all pointing devices in the current user session and persist after exit. raw input applications bypass windows pointer speed/acceleration.

## capabilities

| operation | support | evidence |
| --- | --- | --- |
| detect receiver and usb product name | yes | real raw input enumeration and hid product string |
| windows speed, acceleration, wheel, double-click, swap | yes | native setters followed by native readback |
| save/apply profiles and restore original settings | yes | local json snapshots and verified native apply |
| observed motion report frequency | yes | selected-device raw input delivery timestamps |
| estimate dpi using measured distance | yes | raw counts divided by physical travel in inches |
| current sensor dpi / configured usb polling | unavailable | no verified hardware read command |
| hardware dpi / polling changes | unavailable | no verified vendor command protocol |
| battery, rgb, lod, debounce, button remapping | unavailable | no verified vendor command protocol |

no fake dpi values, timer-resolution tricks, usb overclocking, or claims of improved game latency. `setup` changes desktop cursor behavior; it cannot change sensor sensitivity in a raw input game.

## commands

| command | action |
| --- | --- |
| `status` | live windows readback, receiver identity, explicit unavailable hardware fields |
| `devices` | enumerate all raw input mice |
| `select <index>` | choose one listed device for this interactive session |
| `probe` | read the matching trust hid descriptor capabilities; no device writes |
| `setup` | windows speed 10/20, acceleration off |
| `set speed <1..20>` | change windows pointer speed |
| `set acceleration <on\|off>` | disable acceleration or enable mode 1 with thresholds 6/10 |
| `set wheel <0..100\|page>` | scroll lines or one page per wheel notch |
| `set doubleclick <200..900>` | maximum interval in milliseconds; not click latency |
| `set swap <on\|off>` | swap primary and secondary buttons |
| `measure [3..30]` | capture motion reports; default 10 seconds; escape cancels |
| `calibrate <2..100>` | estimate dpi from a measured straight stroke in cm |
| `profile save <name>` | snapshot current windows settings; overwrites this profile |
| `profile apply <name>` | apply and verify a saved snapshot |
| `profile list` | list saved names |
| `restore` | restore and verify the first pre-change snapshot |
| `faq`, `help`, `clear`, `exit` | reference and navigation |

profile names use 1..32 lowercase letters, digits, underscores or hyphens. the receiver is auto-selected only when exactly one `145f:0326` mouse is present. other devices require explicit interactive selection. enumeration confirms a receiver exists, not that its wireless mouse is powered on.

command mode uses the same batch launcher:

```bat
launch.bat status --json
launch.bat devices --json
launch.bat probe --json
launch.bat setup --json
launch.bat measure 10 --json
```

exit code 0 means success; 1 means invalid arguments, unavailable operation, insufficient measurement data or a native api failure. unsupported hardware fields are `null` in status json. json preserves native device names and paths, including their original casing; the authored console interface is lowercase. calibration requires interactive input and does not support `--json`.

## measurements

for frequency, move the selected mouse continuously in circles for the full capture. timestamps come from a monotonic stopwatch at message processing. the capture loop waits for windows input messages instead of sampling cursor positions on a sleep timer. results show median interval, p95, active mean, report count and idle gaps; a quality message flags median/mean disagreement above 20 percent. gaps above 50 ms are excluded from interval statistics; fewer than 100 usable intervals rejects a result. timing includes usb delivery, windows scheduling and message queue batching. it does **not** read configured usb polling or measure click latency. compare runs during sustained movement under similar system load.

for dpi, mark a distance of 10 cm on your pad. align the mouse axes with that line. start at the first mark, run `calibrate 10`, press enter, move once to the second mark without lifting or returning, then press enter. repeat three times to compare estimates. the calculation uses the dominant net motion axis; obvious diagonal strokes, backtracking and insufficient motion are rejected. it cannot detect every physical measuring mistake or lift. a hardware dpi-button press invalidates the estimate.

status labels saved measurements as history with timestamps, never as current hardware settings. measurements are tied to the selected device path and stored separately from windows profiles.

## recovery and data

the first change creates `%localappdata%\helox-terminal\original.json`. later changes preserve that snapshot. `restore` rolls back to it. each apply also captures its immediate previous settings and attempts rollback if any write or readback fails. a rollback failure is reported explicitly. the original snapshot is retained after restore.

profiles and measurement history stay under `%localappdata%\helox-terminal`. there is no telemetry or network activity in the executable. `devices --json`, `probe --json` and status json include machine-specific device paths; review these before posting diagnostics publicly.

## build and validation

```powershell
powershell.exe -noprofile -executionpolicy bypass -file .\build.ps1
powershell.exe -noprofile -executionpolicy bypass -file .\tests\run.ps1
```

the selftest checks synthetic timing/distance analysis, idle gap rejection, backtracking rejection, profile path validation, serialization, real raw input registration, and real native setting mutations with readback. it restores all original settings in a `finally` block. running it briefly changes system-wide mouse settings. physical dpi calibration and sustained-motion frequency checks still need a person moving the actual mouse; they cannot be validated with injected cursor movement.

see [hardware findings](docs/hardware.md) and [manual verification](docs/verification.md).

## references

- [trust product and adjustable sensor specification](https://www.trust.com/en/product/25307-gxt-929-helox-ultra-lightweight-wireless-gaming-mouse)
- [trust support downloads](https://support.trust.com/en/support/solutions/articles/9000240009-gxt-929-helox-ultra-lightweight-wireless-gaming-mouse-25307)
- [wallhack m-001 configuration guide](https://wallhack.gorgias.help/en-US/wallhack-m-001-dpi-and-polling-rate-customization-guide-6230159)
- [microsoft systemparametersinfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow)
- [microsoft rawmouse: raw input bypasses pointer speed](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawmouse)
- [microsoft raw input overview](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input)

## license

mit. third-party brands and the wallhack interface are not bundled.
