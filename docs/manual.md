# helox terminal

mouse controls for windows, with lowercase text, a numbered menu and a batch launcher. select the mouse you want to measure or configure in `6 more > 1 choose mouse`. input measurements use windows raw input; vendor-specific readings and controls depend on the device.

## start

download the zip from [releases](https://github.com/maximkochergin/helox-terminal/releases), extract it and run `launch.bat`.

```text
  helox / 0.14.1
  ----------------------------------------
  receiver  gxt 929 helox / dpi ? / hz ?
  windows   speed 10/20 / accel on / desktop
  aim       natural / enabled / smooth 8.0 ms

  1  acceleration     2  pointer speed
  3  test hz / gaps   4  check dpi
  5  profiles         6  more
  7  mouse status     8  aim tools
  9  game presets     0  exit
```

type a number and press enter. empty answers, `0` or `back` cancel a prompt. result screens return to the menu with enter or escape. launching alone never changes settings.

`6` > `1` lists mice starting at `1`; `0` goes back. the advanced `devices` / `select <index>` commands use zero-based indices. saving or applying settings keeps the confirmation visible until enter or escape. aim tools remain accessible without a selected mouse for installation and undo; enabling effects still requires a connected selection.

a sole connected raw-input mouse is selected automatically, regardless of brand. with multiple mice, the existing single trust candidate keeps priority; otherwise choose explicitly in `6 > 1`. an explicitly selected mouse is not silently replaced after disconnection.

`1` controls windows acceleration; raw input games bypass it. `8` offers acceleration and smoothing through the official signed raw accel driver. `9` applies [complete game recipes](game-presets.md) for valorant, cs2 and matched kovaak's practice, with combined undo and explicit manual sensitivity/fov steps. `setup` enables windows acceleration and sets pointer speed to 10/20; existing active thresholds are preserved.

## aim tools

open `8` > `5 install driver`, approve windows uac, close the official installer with a keypress, then restart windows once. reopen `launch.bat` > `8`:

- `13 tracking preset`: precision + stability, output smoothing off. keeps your live helox gain limit (or the saved limit / 1.4x fallback) and remembered smooth strength. recent delivery history selects stability just as in `11`; enabling the preset is one verified driver transaction. advanced command: `aim tracking`. this targets correction after fast motion: output averaging can briefly magnify a tiny new-direction step, then suppress subsequent movement. `aim smooth on` restores the remembered output strength if preferred. existing raw accel users do not need to reinstall the driver for these profile changes.
- `14 test response`: reads the current selected profile and simulates example motion without applying settings. shows ratios after 120 repeated 8-count and 800-count inputs, plus a flick-to-micro sequence: 120 horizontal 8-count steps, eight horizontal 800-count steps, then sixteen vertical 1-count steps. output is calculated by the official engine and an integer truncation/fractional-carry model matching the released callback. it reports peak counts and zero outputs; these are simulated counts, not missing radio packets. advanced command: `aim response`; `--json` also exports both output axes, timing, warmup/burst counts and actual profile readback. recent matching delivery history supplies the example interval, with an explicit 8 ms fallback. configured dpi normalization, disabled state, constant interval and time clamps are respected. this does not prove a game's input path or measure physical latency.

response also shows intermediate horizontal examples at 24/80/160 counts per report. json exports `HorizontalSamples` for 1/8/24/40/80/160/400/800 counts, including input counts per processed millisecond and the final floating-point output ratio after 120 repeated reports. every speed starts with fresh simulation state. a 1.4x limit does not mean every motion is multiplied by 1.4: the natural curve stays at 1x below its offset and approaches the limit gradually. physical dpi and in-game sensitivity are not inferred from these examples.
- `1 precision on`: choose steady (1.2x), balanced (1.4x), or flick (1.6x). the natural gain curve stays at 1x at settled slow speeds and progressively approaches the selected fast-motion limit. input offset 3 counts/ms, decay 0.05, baseline input/sensitivity half-lives 4/2 ms. advanced command: `aim precision on <1.1..1.8>`; comma and dot decimals work. `aim precision on` reuses the remembered limit, or 1.4x for older presets. dpi is unknown, so no assumed dpi normalization is applied; your physical dpi and game sensitivity determine the useful transition speeds.
- `11 stability on`: steadier acceleration coefficient. precision must already be on. the chosen strength uses recent same-device delivery history, clamped to 8..12 ms and rounded to 0.5 ms; sensitivity half-life is half that value. natural mode also uses the full value for input-speed half-life. LUT curves keep input-speed averaging off to prevent post-flick correction suppression. missing, stale (>24 h), future (>5 min) or invalid history uses strength 8 ms. `12` restores natural 4/2 ms or LUT 0/0 ms without changing output smoothing. these are helox heuristics, not hardware polling overrides. command: `aim stability on|off`.
- `3 smooth on`: choose light (2 ms), balanced (4 ms), or strong (8 ms). these are output magnitude smoothing half-lives; higher values trade more smoothing for more delay. direction is preserved. half-life is a decay parameter, not an exact latency measurement. `4` switches it off without removing precision or forgetting the strength. advanced command: `aim smooth on 1..12` (integer milliseconds); `aim smooth on` reuses the remembered strength, or 4 ms for older presets. resume also restores the strength.

stability filters the amount of acceleration and delays its response to speed changes, including a return to slow motion after a flick. it preserves current movement direction and does not generate motion at rest. `smooth` separately averages output magnitude and adds movement delay. a sudden small correction after fast movement can inherit a larger magnitude from that average, followed by very small or zero outputs; reduced alternating spread alone does not establish good recovery. neither filter can recover wireless gaps. gain limit, stability and output strength survive precision off/on and resume. precision off bypasses acceleration filters while remembering stability. `aim status` reads all three half-lives; `StabilityEnabled` recognizes natural/legacy 8..12 ms input with half-size scale, or modern LUT input 0 with scale 4..6 ms. `LookupInputSmoothingRisk` flags the older LUT input-speed filter. an explicit curve edit, resume or game recipe rebuilds it with input half-life zero.

- `21 live verify`: temporary actual kernel writes and delayed readback for all filters and game recipes, then full restoration. close games first. interrupted runs retain a snapshot for `aim verify restore`. [scope and recovery](game-presets.md#live-verification).
- `22 remove flick tail`: disables output averaging while keeping the current curve and game sensitivity. this targets the filter's overshoot after fast movement; it does not reconstruct sensor tracking. [movement checks](movement.md).

valorant has used raw input since launch according to [riot's 3.07 notes](https://playvalorant.com/en-us/news/game-updates/valorant-patch-notes-3-07/); the raw input buffer toggle was removed and made always enabled in [11.06](https://playvalorant.com/en-us/news/game-updates/valorant-patch-notes-11-06/). there is no toggle to enable as a helox fix. compare tracking against your previous profile in the practice range with the same dpi button stage and game sensitivity. this tool does not edit game configuration or certify whether a particular match consumed the transformed stream.

[valorant and vanguard notes](valorant.md) explain what the checks establish, likely reasons for a weak perceived effect, and a manual comparison.

settings target the selected hardware id. identical receivers with the same id share the override. existing defaults, profiles and other device overrides are retained; a profile used as the default or shared by another device is cloned before editing. activation is verified by reading the driver after its one-second write delay; no fake success when the backend is missing. failures report whether rollback succeeded. `6 undo aim` restores the complete driver snapshot from before the first helox aim change, including other devices. windows settings have their own restore in `5`.

effect switches retain the selected device's existing dpi normalization and timing parameters, including settings inherited from the driver's defaults. off keeps a bypassed device bypassed; on, tracking and resume explicitly enable it. bypassed status labels the displayed values as configured only. readback and response use the kernel's exact hardware-id match. repeating an already restored undo clears saved choices without another activation or filter reset.

driver settings reset on reboot: use `8 resume saved` in aim tools or `aim resume` to reapply the last choices for this mouse. no background process or startup task is added. desktop movement also passes through this driver; windows acceleration can additionally affect the desktop. raw input games use the driver-transformed counts. dpi estimation is blocked when a detected raw accel filter transforms counts, including its input speed cap, or when an installed raw accel driver cannot be inspected. an unavailable filter state is unknown rather than silently treated as off.

the optional installer downloads [raw accel 1.7.1 from its official release](https://github.com/RawAccelOfficial/rawaccel/releases/tag/v1.7.1), checks a pinned sha256 and the driver signature. no third-party binaries are bundled. `aim prepare` downloads and verifies without installing. [mechanics, research and verification](aim.md).

### driver checks and removal

`8 > 9 check driver`, `6 > 4`, or `aim doctor` checks backend files against the pinned official archive, service registration, the mouse class filter, installed driver hash/signature, pending driver file, control endpoint and live protocol readback. no selected mouse is needed and no settings are applied. `aim doctor --json` exports the same checks. native readback runs only after backend preparation and both archive/backend verification succeed; otherwise `KernelReadable` is null and the endpoint check remains independent. every first aim bridge load also verifies its files, including menu/status paths. missing or inaccessible evidence remains unknown; a registered service alone does not establish that the driver is active. the upstream 1.7.1 package uses protocol 1.7.0.

with a selected mouse, doctor and status also read its pnp instance id, started/problem state and windows-reported device stack through configuration manager. `raw accel listed` refers to that stack, rather than just a global service or class-filter registration. json exposes `SelectedDeviceStack` in doctor and `selectedDeviceStack` in status. missing selection gives null; unavailable, unsupported or malformed stack properties leave presence unknown. this check does not certify vanguard compatibility, game consumption or wireless mouse power. instance resolution uses the interface property instead of assuming every mouse is a hid instance.

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

`8 > 15` opens the [personal curve builder](curves.md): base sensitivity, start/end speeds, fast/base limit and transition shape. preview runs the proposed profile through the official engine without activation. apply writes, checks and saves the selected device's curve. component switches preserve it; an explicit natural gain returns to natural acceleration.

`8 > 16` offers optional axis angle snapping, initially off. the 0..5 degree range is a helox limit, not riot certification. this direction filter is independent of precision and output smoothing. [behavior and policy limits](curves.md#angle-snapping).

`6 > 7` and `8 > 17` read application crashes and shutdown timing without changing settings. [coverage](curves.md#shutdown-popup).

`8 > 18` attenuates directions independently; `8 > 19` softly reduces all slow movement, including wanted corrections. both offer preview before apply and start off. `8 > 20` bypasses every raw accel effect for the selected mouse, even with damaged saved controls. [filter behavior, interactions and recovery](filters.md).

```text
setup
aim prepare
aim install
aim status
aim events
aim curve
aim curve preview 1 3 30 1.4 1
aim curve apply 1 3 30 1.4 1
aim curve natural
aim snap on [0..5]
aim snap off
aim response
aim verify
aim verify restore
preset list
preset show|preview|apply <name>
preset undo
preset recover
aim tracking
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

the full suite temporarily changes windows preferences and restores them. regressions cover acceleration preservation, queued input timing, calibration timeout, three-pass dpi analysis, descriptor/device scoping, menu navigation, profile validation and input recovery. `tests\live.ps1` separately performs real reversible driver writes, game recipe apply/undo and persistence-failure recovery; close games before running it. [evidence and limits](verification.md). physical mouse-body/ruler measurements still need manual verification.

source builds compile into a temporary directory and replace the executable only after successful compilation. compiler errors keep the previous executable. if replacement is blocked by a file lock, close helox and retry; the previous executable remains intact.

[hardware findings](hardware.md) / [verification](verification.md) / [windows raw input](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawmouse)

## license

released under the [mit license](../LICENSE). you may use, modify and distribute this software, including for commercial projects, provided you retain the copyright notice and license text. the software is provided as is, without warranty. see [LICENSE](../LICENSE) for the full terms.

the optional raw accel backend is a separate project under its own mit license; its downloaded package retains its license and notices. helox presets and integration are maintained here.
