# helox terminal

minimal white-text windows cli for the trust gxt 929 helox. lowercase controls, numbered menu, batch launcher.

## start

download the zip from [releases](https://github.com/maximkochergin/helox-terminal/releases), extract it and run `launch.bat`.

```text
  helox / 0.9.0
  ----------------------------------------
  receiver  gxt 929 helox
  windows   speed 10/20 / accel on
  hardware  dpi ? / hz ?

  1  acceleration     2  pointer speed
  3  test hz / gaps   4  check dpi
  5  profiles         6  more
  7  mouse status     8  aim tools
  0  exit
```

type a number and press enter. empty answers, `0` or `back` cancel a prompt. result screens return to the menu with enter or escape. launching alone never changes settings.

`6` > `1` lists mice starting at `1`; `0` goes back. the advanced `devices` / `select <index>` commands use zero-based indices. saving or applying settings keeps the confirmation visible until enter or escape. aim tools remain accessible without a selected mouse for installation and undo; enabling effects still requires a connected selection.

`1` controls windows acceleration; raw input games bypass it. `8` offers game-wide acceleration and smoothing through the official signed raw accel driver. `setup` enables windows acceleration and sets pointer speed to 10/20; existing active thresholds are preserved.

## aim tools

open `8` > `5 install driver`, approve windows uac, close the official installer with a keypress, then restart windows once. reopen `launch.bat` > `8`:

- `1 precision on`: choose steady (1.2x), balanced (1.4x), or flick (1.6x). the natural gain curve stays at 1x at settled slow speeds and progressively approaches the selected fast-motion limit. input offset 3 counts/ms, decay 0.05, baseline input/sensitivity half-lives 4/2 ms. advanced command: `aim precision on <1.1..1.8>`; comma and dot decimals work. `aim precision on` reuses the remembered limit, or 1.4x for older presets. dpi is unknown, so no assumed dpi normalization is applied; your physical dpi and game sensitivity determine the useful transition speeds.
- `11 stability on`: steadier acceleration coefficient, using input-speed and sensitivity ema filters rather than output smoothing. precision must already be on. input half-life uses the last valid median delivery interval for this mouse, clamped to 8..12 ms; sensitivity half-life is half that value. history older than 24 hours, over five minutes in the future, missing or invalid uses 8/4 ms. this is a helox heuristic, not a manufacturer recommendation or polling override. `12 stability off` restores baseline 4/2 ms without changing your gain limit or `smooth` strength. advanced command: `aim stability on|off`.
- `3 smooth on`: choose light (2 ms), balanced (4 ms), or strong (8 ms). these are output magnitude smoothing half-lives; higher values trade more smoothing for more delay. direction is preserved. half-life is a decay parameter, not an exact latency measurement. `4` switches it off without removing precision or forgetting the strength. advanced command: `aim smooth on 1..12` (integer milliseconds); `aim smooth on` reuses the remembered strength, or 4 ms for older presets. resume also restores the strength.

stability filters the amount of acceleration and delays its response to speed changes, including a return to slow motion after a flick. it preserves current movement direction and does not generate motion at rest. `smooth` separately averages output magnitude and adds movement delay. neither can recover wireless gaps. gain limit, stability and output strength survive precision off/on and resume. precision off bypasses both acceleration filters; a remembered stability choice takes effect when precision is enabled again. `aim status` reads the actual gain limit and all three half-lives from the driver; json adds `InputHalfLifeMs`, `ScaleHalfLifeMs`, and `StabilityEnabled`. the stability label reflects the enabled natural curve with the 8..12 ms input / half-size scale pair, including matching settings applied outside helox.

settings target the selected hardware id. identical receivers with the same id share the override. existing defaults, profiles and other device overrides are retained; a profile used as the default or shared by another device is cloned before editing. activation is verified by reading the driver after its one-second write delay; no fake success when the backend is missing. failures report whether rollback succeeded. `6 undo aim` restores the complete driver snapshot from before the first helox aim change, including other devices. windows settings have their own restore in `5`.

driver settings reset on reboot: use `8 resume saved` in aim tools or `aim resume` to reapply the last choices for this mouse. no background process or startup task is added. desktop movement also passes through this driver; windows acceleration can additionally affect the desktop. raw input games use the driver-transformed counts. dpi estimation is blocked when a detected raw accel filter transforms counts, including its input speed cap, or when an installed raw accel driver cannot be inspected. an unavailable filter state is unknown rather than silently treated as off.

the optional installer downloads [raw accel 1.7.1 from its official release](https://github.com/RawAccelOfficial/rawaccel/releases/tag/v1.7.1), checks a pinned sha256 and the driver signature. no third-party binaries are bundled. `aim prepare` downloads and verifies without installing. [mechanics, research and verification](aim.md).

### driver checks and removal

`8 > 9 check driver`, `6 > 4`, or `aim doctor` checks backend files against the pinned official archive, service registration, the mouse class filter, installed driver hash/signature, pending driver file, control endpoint and live protocol readback. no selected mouse is needed and no settings are applied. `aim doctor --json` exports the same checks. native readback runs only after backend preparation and both archive/backend verification succeed; otherwise `KernelReadable` is null and the endpoint check remains independent. every first aim bridge load also verifies its files, including menu/status paths. missing or inaccessible evidence remains unknown; a registered service alone does not establish that the driver is active. the upstream 1.7.1 package uses protocol 1.7.0.

`8 > 10 uninstall driver` and `6 > 5` perform the same removal. type `uninstall` to continue, or `0` to cancel. advanced command: `aim uninstall`. this affects raw accel for all mice and other raw accel apps; helox profiles, backups and downloads stay available. the verified official uninstaller removes the filter and driver file. helox then removes the remaining service only if its type and image match the expected raw accel driver. windows uac and the official keypress window are required. the filter/file/service results are checked rather than trusting the upstream exit code alone.

restart windows after removal: a loaded driver and files queued for deletion can remain until then, as described in the [official guide](https://github.com/RawAccelOfficial/rawaccel/blob/v1.7.1/doc/Guide.md#installation). `aim doctor` can report an open endpoint even after removal. no force-unload or automatic restart is attempted.

### reset helox

`6 > 6 reset all helox data + driver` requires typing `reset`. advanced command: `cleanup --confirm`. the current session closes and a separate cleanup window reports the result. close other helox sessions first.

reset restores the original windows snapshot if present and the original aim snapshot if backed up and the driver is loaded. it then uninstalls the shared raw accel driver and deletes `%localappdata%\helox-terminal`: profiles, original/undo/aim backups, saved presets, dpi/hz history, downloaded backend and temporary cache files. a missing original windows snapshot leaves windows preferences unchanged; a failed restore stops cleanup. backups are permanently deleted after successful removal. extracted application files stay in their original folder and can be deleted after closing the cleanup window.

cleanup refuses junctions/symlinks, other helox sessions, unexpected roots and locked/inaccessible files. it checks delete access for the complete tree and validates backups that will be restored before changing settings or uninstalling the driver, then repeats access checks before deletion. failure to restore or uninstall keeps the data; filesystem errors after deletion begins can leave a partial tree and are reported. new helox sessions are blocked while cleanup runs, including json commands, which return a json error. restart if the driver was installed, then repeat the driver check if needed.

## what works

- actual receiver detection, driver/revision/descriptor dossier and live windows setting readback.
- windows pointer speed, acceleration, scrolling, double-click interval and primary button swap.
- profiles and original-setting restore; writes are checked and rolled back on failure.
- observed input frequency for one selected mouse, and distance-based dpi estimates.

hardware dpi/polling writes, current sensor dpi, configured polling, battery and rgb remain unavailable. `?` means unknown. use the physical dpi button. receiver presence does not confirm mouse power or wireless link.

## tests and recovery

`3` captures 10 seconds of continuous mouse movement. reported hz uses average active report delivery, which avoids enormous median-based values caused by queued batches. p95/p99 intervals, slow intervals, long gaps and maximum gap expose uneven delivery. long gaps are excluded from active hz but remain visible; a pause cannot be distinguished from a wireless interruption. this is an input measurement, not configured usb polling or click latency.

for wireless stutters, repeat the capture without stopping and compare with the receiver close to the mouse, away from active usb 3 devices/cables. [intel's measurements](https://www.benq.com/content/dam/newb2b/Support/FAQ/intel_white%20paper_usb3%20interference.pdf) document 2.4 ghz interference and receiver placement effects. smoothing cannot recreate missing packets or raise hardware polling rate.

repeat tests show the change in active hz and p95 interval against the last valid test of the same mouse. positive p95 means longer intervals; negative means shorter. use similar motion for both tests. json exposes these deltas in `Comparison`; no baseline gives null. invalid history is excluded from status and comparisons.

comparison also includes p99, gap-time share, and slow-interval share when both tests contain those metrics. shares are reported in percentage points (`pp`), so different report counts do not make raw outlier counts misleading. batching and pauses add a context note rather than an automatic better/worse verdict. json additionally exports span-frequency change as `DeliveredHzDifference`. missing older metrics stay null. slow shares use each run's own displayed threshold; compare similar motion and settings.

the test also shows delivery frequency including long gaps and the percentage of the sampled span spent in gaps over 50 ms. this span starts at the first motion report and ends at the last; time before/after movement is excluded. a pause and interrupted wireless delivery remain indistinguishable. slow intervals use a displayed heuristic threshold of `max(1.75 * median, median + 0.25 ms)`, so a 16 ms interval among 8 ms reports is now detected. this is a timing outlier count, not proof of lost hardware packets. json adds `SpanMs`, `GapDurationMs`, `DeliveredHz`, `GapPercent`, and `SlowThresholdMs`; older history leaves them null.

`4` checks approximate dpi without a ruler or pad marks. keep a fingertip beside the front edge of the mouse, then slide the mouse forward until the rear edge reaches that same stationary fingertip. this gives one mouse-body length of travel. press enter to start and finish each pass. do not rotate, lift or return during a pass. repeat three times; the app uses their median and rejects a spread above 15 percent. the gxt 929 body length is 125 mm according to the manufacturer. repeatability is not absolute accuracy: hand alignment and actual travel still matter.

this is an estimate of delivered raw counts per inch, not a sensor setting read. an external input filter can change those counts. exact automatic dpi cannot be derived from counts alone when the device provides no physical scale or readable setting. the connected mouse's x/y hid descriptors have no distance units or physical range. known-distance calibration remains available as `calibrate <cm>`; comma and dot decimal separators work.

raw accel filter state is checked before and after each dpi pass, including after waiting to start. an active or unreadable filter aborts without saving a new estimate. these boundary checks do not monitor third-party changes throughout a stroke; keep filter settings unchanged during measurement.

`7` shows the detailed dossier. receiver identity, driver metadata and hid values are read locally; model dimensions/range/stage count come from [trust's product specifications](https://www.trust.com/en/product/25307-gxt-929-helox-ultra-lightweight-wireless-gaming-mouse). driver-declared button count and sample rate are labeled separately from physical controls and measured frequency. stored test results include timestamps in detailed status; unknown battery, current dpi, configured hz and mouse power stay unknown.

`5` saves/loads windows profiles or restores the original snapshot. data stays in `%localappdata%\helox-terminal`. history is labeled as history, never current hardware values. there is no telemetry.

saved profiles are selected by number when loading.

`5` > `4` or `undo` reverses the last successful windows settings change, including a profile load or original restore. a second undo reverses that undo. unchanged settings preserve the existing undo snapshot. if saving the undo snapshot fails, the tool rolls the change back and reports any rollback failure. aim driver settings keep their separate restore command.

`5` > `5` or `profile show <name>` shows only settings that would change, as `current -> saved`. identical profiles show `already matches / no changes`. acceleration modes, both thresholds, page scrolling and swapped buttons are included. this reads current preferences without applying the profile; another app can still change them after the preview. `--json` continues to export the complete saved snapshot. saving profiles shares the settings lock so another helox instance cannot write halfway through the snapshot.

profiles must contain a complete, valid settings snapshot. a damaged original backup blocks new changes so recovery is not silently lost. simultaneous cli instances serialize settings changes and read current values after acquiring the lock.

## advanced commands

```text
setup
aim prepare
aim install
aim status
aim doctor
aim uninstall
cleanup --confirm
aim precision on|off
aim precision on <1.1..1.8>
aim stability on|off
aim smooth on|off
aim smooth on <1..12 ms>
aim restore
aim resume
check
set acceleration on|off
set speed 1..20
set wheel 0..100|page
set doubleclick 200..900
set swap on|off
measure 3..30
dpi
calibrate <cm>
profile save|show|apply <name>
profile list
undo
restore
devices
select <index>
probe
status
faq
home
```

command mode: `launch.bat status --json`, `launch.bat probe --json`, `launch.bat measure 10 --json`. exit code 0 means success; 1 means failure. unsupported hardware values are null. diagnostics include machine-specific device paths; review before sharing.

text commands inside the terminal also accept `--json`; the format applies to that command only. use batch command mode for a pure json stream. repeated aim choices that already match the live driver save the preset without activating the same configuration again.

## build

windows 10/11 with .net framework 4.x. basic features require no administrator account, python, npm or startup task. optional aim tools require 64-bit windows, .net framework 4.7.2+, the visual c++ 2019 x64 runtime, administrator approval for driver installation and one restart. the [official installation guide](https://github.com/RawAccelOfficial/rawaccel/blob/master/doc/Guide.md#installation) links prerequisites. from source, `launch.bat` compiles on first use. after source edits or pulling updates, rebuild with `build.ps1`.

```powershell
powershell.exe -noprofile -executionpolicy bypass -file .\build.ps1
powershell.exe -noprofile -executionpolicy bypass -file .\tests\run.ps1
```

tests temporarily change native settings and always restore them. regressions cover acceleration preservation, queued input timing, calibration timeout, three-pass dpi analysis, descriptor/device scoping, menu navigation, profile validation and input recovery. physical mouse-body/ruler measurements still need manual verification.

source builds compile into a temporary directory and replace the executable only after successful compilation. compiler errors keep the previous executable. if replacement is blocked by a file lock, close helox and retry; the previous executable remains intact.

[hardware findings](hardware.md) / [verification](verification.md) / [windows raw input](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawmouse)

## license

released under the [mit license](../LICENSE). you may use, modify and distribute this software, including for commercial projects, provided you retain the copyright notice and license text. the software is provided as is, without warranty. see [LICENSE](../LICENSE) for the full terms.

the optional raw accel backend is a separate project under its own mit license; its downloaded package retains its license and notices. helox presets and integration are maintained here.
