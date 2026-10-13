# verification

## final operation and recovery / 0.20.0

the final checks distinguish pure managed regressions, native-engine calculations, interactive Windows preference tests, real driver integration and release-delivery fixtures. a successful configuration readback does not establish physical sensor accuracy, game camera behavior or anticheat acceptance.

local release checks on 2026-10-13:

| check | result and scope |
| --- | --- |
| `tests/run.ps1` | passed: official native engine, Windows preference mutation/readback/restoration, managed checks, command/menu/profile/undo contracts and script/launcher/package fixtures |
| `tests/final-live.ps1` | passed: forced exit during a desktop write recovered; ten startup cases; complete pre-test driver, Windows, saved-file and owned HKCU Run state restored |
| expanded `tests/live.ps1` | passed: 13 real driver cases, 21 recipe apply/undo cycles, independent smoothing/tail repair/control recovery, stale-preview refusal, persistence/forced-exit recovery and malformed-controls escape hatches |
| inherited PowerShell 7 module-path case | skipped: `pwsh.exe` unavailable; the conditional test did not reproduce this environment locally |
| actual Windows restart/sign-in | not performed; hidden-helper execution and simulated cold driver state were tested separately |
| physical sensor/game comparison | not performed; `GameInputVerified` remains null |

the ten startup cases include failed `startup off` with a read-only file and registration rollback, hidden already-matching execution, factory-default driver restoration, captured DPI 1600/1000 Hz constant calculation timing retention, latest saved snapping, and skips for bypass/external state, pending recovery, a disconnected mouse and disabled registration. DPI and Hz here are software calculation parameters, not hardware readings. `DesktopForcedExitRecovered` and `FullStateRestored` were true; `ActualWindowsSignInTested` was false.

expanded live integration also verified idempotent reapply, preference-only undo, generic precision/bypass/original-restore hard exits, mutation guards, byte-exact recovery of malformed saved controls and refusal of corrupt undo. `GenericControlForcedExitRecovered`, `MalformedControlsEscapeHatches`, `BlockingActivationVerified` and the other recovery checks passed. synchronous native activation took 1007 ms in this run, with immediate full readback verified; this is a configuration-write delay, not input latency. the success summary was emitted after restoring the complete original driver/Windows state and saved-file bytes. the passive capture contained zero motion reports. no physical sensor improvement or game input was verified; `GameInputVerified` remained null.

new managed regressions cover interrupted desktop writes and prior undo restoration, checkpoint retention on failed recovery, persistent mouse-selection validation, conservative startup policy, opaque byte-exact aim recovery and malformed raw-input captures/history. legacy version-1 game checkpoints remain strictly validated; version 2 permits damaged saved controls to be restored as exact bounded bytes without interpreting them. complete driver/windows snapshots remain validated in both formats.

delivery fixtures check both executable outputs, retained binaries on compilation/replacement failure, concurrent build exclusion, hidden helper GUI subsystem, launcher paths/arguments/exit codes, package version consistency, the zip/hash pair, file manifests and extraction tampering. backend fixtures check pinned offline preparation, locked file reuse/refusal, upstream-settings retention and junction refusal. real driver removal and user-data purge are not performed by these fixtures.

`tests/commands.ps1` provides hosted-safe read-only contracts for version, status, health, startup registration/recovery fields and malformed-command refusal. it compares live Windows and aim state before/after without settings writes, registry writes or driver installation. `tests/final-live.ps1` is a separate local test with games closed; it temporarily writes driver state, Windows preferences and the project's HKCU Run value, then restores them. it is excluded from hosted CI.

startup integration distinguishes already-matched no-op, factory-default driver restoration and refusal to overwrite a custom/global/peer/bypassed configuration. manually executing the helper or simulating factory defaults is not an actual Windows restart/sign-in test. the helper runs once and has no retry loop. check its last result after the first actual sign-in.

safe-game checks cover detected supported process names only. they do not verify unknown games or eliminate a game-start race after the query. bypass is live state rather than saved resume metadata: an enabled startup helper can restore the enabled saved profile after a cold driver reset. disable startup before bypassing when that disabled effect state should persist across sign-ins.

the inherited PowerShell 7 module-path regression is conditional on `pwsh.exe`; an unavailable PowerShell 7 installation is a skip, not a passing reproduction. older sections below refer to the named historical versions.

## flexible game effects and input history / 0.19.0

pure regressions cover every effect override, immutable drafts, honest recipe descriptions, repeatable CLI arguments, duplicate/invalid options, disabled micro-strength retention, history scoping and freshness, and successful/failed running-game queries. native-engine regressions exercise combined smoothing, stability, damping, snapping and direction scales at 1/2/4/8 ms output half-lives; processing remains valid and the strong-smoothing tail is reported separately.

the command/menu suite checks an editable 2 ms draft, read-only comparison with the base and live setup, cancellation back to the retained draft, invalid apply options, same-mouse saved history and legacy optional fields. these tests do not launch games or inject input.

local live integration passed 13 driver cases and 21 recipe apply/undo cycles, including customized effects for valorant, cs2 and matched kovaak's practice. checks cover warning refusal before mutation, complete native effect readback, stale driver/windows/effect/game previews, repeat apply with read-only matching preferences, save-failure rollback and forced-exit recovery. the full native driver and windows snapshot was restored. this run captured no motion reports; physical sensor improvement and game input remain unverified.

## independent smoothing and exact control recovery / 0.18.0

pure regressions check curve-table preservation, native caps, rotation, snapping, direction scales, normalization, constant timing, shared/default profile isolation, bypass, no-op identity and all 15 built-in recipe matches. unknown curves are refused by metadata recovery. output smoothing changes only its half-life; flick-tail repair additionally clears legacy input averaging for LUT curves, retaining scale stability. mixed per-axis timing is preserved.

local live integration checks passed 13 driver cases and 18 game-recipe apply/undo cycles. an imported natural curve with a native input cap now accepts smoothing on/off while retaining the cap and all other fields. legacy LUT input/output averaging is cleared with unchanged curve, scale stability and windows settings; the native engine model shows the corresponding tail reduction. stale previews are refused before changes. exact metadata recovery leaves the full native/windows state unchanged and preserves peer saved controls. read-only preference-file failures roll back driver writes and restore saved files. pending recovery blocks new filter/recovery writes; forced process exits and explicit recovery are covered by the shared transaction tests.

these are configuration and processing checks. 68 raw-input motion reports were captured during the final live run; no physical before/after sensor comparison was performed and no game was launched. sensor improvement and game input remain unverified.

## personal range, saved choices and shell setup / 0.17.1

personal tuning previously discarded report intervals below 0.25 ms. it now sums path length over windows of at least 1 ms, retaining high-rate reports, reversals and same-timestamp batches. synthetic 125 / 1000 / 4000 / 8000 hz examples check counts per elapsed time and contributing report counts; these are fixtures, not hardware-rate measurements. non-monotonic, non-finite, empty and insufficient captures remain rejected.

reapplying a recipe when native settings already matched could overwrite saved choices without replacing undo. live integration checks cover both altered and missing selected-mouse preferences, exact byte restoration, unchanged native settings and preservation of undo on a subsequent true no-op. a pending recovery now takes precedence over missing personal tuning. the menu reports invalid personal tuning and returns to a built-in draft without applying it.

when an active helox LUT has lost its saved metadata, status now explains why component switches are blocked and lists explicit curve/recipe recovery and bypass. the guards remain in place; the command suite accepts this expected refusal and compares the live driver before and after previews.

Windows PowerShell launched through a non-PowerShell process can inherit PowerShell 7 module paths, breaking hash and signature commands. setup and maintenance prefer their host's built-in modules. a separate regression reproduces the inherited environment when PowerShell 7 is available and checks hash, signature and archive command recovery; driver installation/removal is not part of that test.

## selected mouse and imported profiles / 0.15.1

regressions cover default selection of a sole non-trust mouse, refusal of ambiguous/disconnected devices, and the preview's mouse identity guard. recipe matching accepts an equivalent renamed profile and ignores unrelated default-profile edits; bypass and altered device timing still fail the match. smoothing rejects changes that would replace imported curve, axis, rotation or cap settings before backups, preferences or driver writes. real integration checks exercise normal smoothing on/off around a recipe and a rejected imported cap with full native and saved-file comparisons.

## flick recovery and live recipe matching / 0.15.0

the response model now includes a horizontal flick followed by sixteen opposite one-count reports, alongside the perpendicular correction case. removing output averaging preserves settled slow/fast response in the natural curve at 1, 8 and 16 ms intervals. corrected turn peaks are 2, 1 and 1 counts respectively; reversal peaks are 1 count with no wrong-direction output. fractional carry can cancel the first opposite count at 8 and 16 ms, so that is reported explicitly rather than described as zero lost counts.

tests check every recipe's live configuration match and reject a bypassed recipe as active. local integration tests check the same match after real apply/readback. command tests cover cancellation and returning from preview to apply. these are processing and configuration checks, not a sensor spinout repair or a measurement inside a game.

## interrupted preset transactions / 0.14.1

- apply and undo persist a validated complete recovery snapshot before native or preference changes. validation covers previous desktop undo and saved-control metadata; invalid files stop preflight.
- mocked failures verify independent driver/windows/file recovery, checkpoint retention on any failed component, and no native reset when Windows failed before activation or unchanged-state persistence failed.
- the local live suite kills actual apply and undo processes after their Windows writes, checks mutation guards, restores each combined snapshot and retries undo. another test measures the synchronous native activation delay and verifies the newly activated configuration immediately after return, without the old extra 1.2-second sleep.
- all five live apply/repeat/undo paths, persistence-failure recovery and live verification remain covered. the test helper clears recovery evidence only after both native components are restored.

## live driver and game recipes / 0.14.0

- the native LUT flick reproducer emits three zero reports with legacy input-speed EMA and none after the fix; all five recipes are checked at 1/8/16 ms intervals for finite bounded output and correction direction.
- native scale-only LUT stability reduces alternating gain spread from 0.157x to 0.123x / 0.114x / 0.106x at selected strengths 8 / 10 / 12 ms. direction and rest checks pass. legacy and modern settings retain strength through explicit migration and component switches.
- complete recipes clear previous snapping, directional bias and output averaging, define software normalization/timing and preserve default/peer configurations. paired trainer and target controls are identical.
- transaction checks inject windows, driver, file and rollback failures. recovery attempts every component and retains original and recovery errors. exact file bytes, previous absence and unchanged read-only files are covered.
- CLI tests cover all five definitions/previews, the new numeric menu/cancellation, malformed commands and finite direction-sample interval bounds.
- `tests/live.ps1` is a separate local test requiring a started signed backend and closed games. it performs 13 real driver write/readback cases with restoration, applies/repeats/undoes all five recipes, and forces a real preference write failure after activation to verify combined recovery. a pending verification snapshot blocks new aim/recipe writes and supports verified restoration. it is not a CI test and does not inject input.

kernel readback, raw-input registration and native model response are separate evidence. actual hand transformation, game camera behavior, aim-score improvement and vanguard approval are not proven by these tests.

## personal curves / 0.12.0

- native tests compare lookup interpolation and capped high-speed output against the generated float table, including curve parameter extremes.
- native serialization retains the table; snapping-off direction and rest behavior are checked separately from optional horizontal/vertical snapping and unaffected diagonals.
- pure checks cover curve persistence, legacy defaults, component switches, explicit natural reset and rejection of stale externally modified tables.
- cli checks cover preview/cancel without changing live settings, invalid curves/angles, and journal json.
- event diagnostics use bounded read-only windows logs. absence of a matching application error is not proof that a shutdown popup or kernel fault did not occur.

## automated on windows

`launch.bat check` runs analysis, preset validation and recovery checks without native setting changes or an installed aim backend. github actions runs this command on every push and pull request. `selftest` additionally tests native windows preferences and the available aim engine on an interactive desktop.

- source compiles using the installed .net framework c# compiler.
- native speed, acceleration, scroll lines, double-click timing and swapped-button settings accept changes and read back correctly.
- all pre-test settings are restored and read back after those mutations.
- raw input mouse enumeration and a hidden message-only input sink register successfully.
- hid descriptor inspection finds both the actual trust vendor and mouse collections.
- synthetic 1000 hz and 125 hz sequences produce expected analysis values; idle gaps are excluded; empty captures are rejected.
- 3150 counts over 10 cm produces approximately 800.1 dpi; substantial backtracking is rejected.
- profiles round-trip settings and reject traversal names.
- command-mode status json keeps unknown hardware dpi and polling fields null.
- out-of-range speed returns exit code 1 before writing settings.
- setup enables acceleration and preserves active mode 2/custom thresholds; enabling twice does not reset them.
- simulated queued input batches produce average observed hz rather than an inflated median-based headline.
- calibration timeout rejects incomplete captures; comma/dot distances both parse correctly.
- numeric menu navigation, cancellation, invalid number recovery and a compact home screen pass redirected-input checks.
- negative wheel values are rejected unless the explicit `page` keyword is used.
- incomplete profiles and noninteger setting fields are rejected before native writes; atomic overwrite leaves no temporary files.
- short event bursts and invalid timestamps cannot produce a saved frequency estimate; off-axis zigzag strokes are rejected.
- cancelled numeric prompts redraw the main menu; unsupported json commands return a json error, and command casing is handled consistently.
- three body-length dpi passes produce the expected median/counts; a spread above 15 percent is rejected.
- dossier reads metadata for the selected device family, excludes unrelated hid collections and exposes known-model source information.
- detailed status opens from menu item 7; interactive dpi checks reject redirected/json use.
- official aim engine tests execute after `aim prepare`: slow 1x, capped fast-motion gain, reduced magnitude variation, direction preservation and unrelated device/profile retention.
- aim status returns a backend state; activation without an installed driver fails without claiming an applied preset.
- long gaps remain visible in diagnostics even when excluded from active hz.
- equal-timestamp reports count towards delivered frequency; batched delivery remains flagged.
- default/shared aim profiles are preserved, and repeat updates retain profile order.
- driver readback compares values independently of object key order; changed values and array order are detected.
- simulated apply/rollback failures preserve the original failure and report recovery success or failure.
- input speed caps are recognized as count transforms; windows reserved profile filenames are rejected.
- installer preparation recovers a corrupt cached zip, removes temporary downloads and rejects a parallel setup before extraction; no kernel installation is required for these checks.
- preparation also succeeds while the cli's native bridge and json assembly are loaded: verified identical files are retained instead of overwriting locked dlls.
- saved aim choices round-trip; null, incomplete and mistyped presets are rejected.
- gain limits 1.1/1.2/1.4/1.6/1.8x produce ordered, bounded settled acceleration in the released engine, with 1x at settled slow input.
- stability at 8/4 ms reduces alternating acceleration ratio spread at 8 ms delivery intervals, preserves direction and produces no movement at rest.
- gain and stabilization survive off/on and resume; stability requires precision and never changes output strength, dpi/polling defaults or peer overrides.
- delivery-based stabilization ignores invalid, stale, future or unrelated history and clamps its input half-life to 8..12 ms.
- new aim field types/ranges and invalid command strengths are rejected; precision and smoothing menu cancellation leave live settings unchanged.
- the released engine plus callback carry model reproduces an 8 ms output-averaging flick-to-micro transient: 72-count peak and four zero outputs. tracking produces a one-count peak and no zeros for the same example.
- response simulation honors disabled and dpi-normalized devices, constant timing and callback clamp order. fractional counts are retained on both axes; invalid/overflowing outputs are rejected.
- tracking retains the gain and remembered output strength through resume; response json/menu checks leave actual driver settings unchanged.
- effect switches preserve per-device and inherited normalization/timing; off cannot activate a bypassed device, and explicit enable retains its other options.
- status and response match the kernel's exact device ids, including default profile/timing fallback for a differently cased override.
- repeated aim restore clears saved choices without activating an already matching snapshot or resetting filter state.
- response samples eight horizontal speeds, retains existing 8/800-count ratios and checks the intermediate natural-curve region, disabled bypass and dpi normalization with the official engine.
- selected-device stack diagnostics validate native property types, sizes, utf-16 termination, exact service names and bounded retries; unavailable evidence stays unknown.
- the home screen distinguishes desktop acceleration from the configured aim profile without adding menu items.
- analysis-only checks leave windows preferences unchanged; missing-driver resume cannot claim success.
- malformed driver snapshots are rejected before loading the bridge: nonfinite/string numbers, overlong or nul-containing names, duplicate profiles/device ids, missing profile references, invalid clamps/integers and lookup float overflow.
- driver presence considers its live control endpoint as well as the service registry; inaccessible endpoints fail closed rather than enabling dpi estimation.

## manual pass

v0.13 filter review additionally checks:

- remembered snapping strength across off/on, legacy presets and strict saved filter validation.
- rejection of externally changed lookup interpretation/layout and changed saved speed units, including resume and precision off/on.
- native directional sign/order and diagonal scaling; native float serialization of combined damping tables, sorted knots, capacity and flat tails.
- native natural formula agreement, a 1025-speed logarithmic approximation sweep for 1.1/1.4/1.8x limits and 0.1/1/20 recovery speeds, and preservation of the personal table above recovery.
- 36 combinations of smoothing, stability and snapping at 1/8/16 ms example intervals, with damping and asymmetric direction scales. every combination has a bypass comparison.
- rest behavior and fractional carry for 128 repeated one-count inputs with strong damping.
- selected-device bypass scope, inherited defaults and ignored case aliases; no saved-preset or backup dependency in the bypass transaction.
- read-only direction/micro/curve previews, proposed-profile labels, invalid command rejection and menu cancellation. complete live configuration is compared before and after local verification.
- unavailable/incomplete shutdown journals leave unmatched proximity unknown.

these are calculation and regression checks. no new filter is activated on the user's mouse during tests; physical aim feel and concurrent vanguard/game compatibility require a manual comparison. the signed kernel binary is unchanged.

1. double-click `launch.bat`. confirm white lowercase interface, actual mouse product name and live settings.
2. run `profile save before`. run `setup`. confirm acceleration is on, existing thresholds/mode are preserved, and the desktop cursor response changes where applicable.
3. change `set speed 6`, then `set speed 15`. compare desktop response. raw input games should retain their own sensitivity.
4. run `restore`. confirm settings equal the original snapshot. use `profile apply before` if testing from a later already-customized session.
5. run `measure 10` three times while moving continuously. compare average observed delivery, not a supposed configured hardware value. json includes median hz as a separate diagnostic.
6. run `calibrate 10` three times against ruler marks. compare estimates. press the physical dpi button, recalibrate, and compare the new estimate; the saved history is never automatic hardware readback.
7. power the mouse off while leaving the receiver attached. status may still enumerate the receiver; a capture without motion should fail without saving a result.
8. select the trust device and disconnect its receiver. measurements must report the missing selected device, without switching to the touchpad.
9. in the legacy console, click/drag during a test. capture should keep running; the original quick-edit mode should return afterward.
10. open `3 test mouse > 3 dpi estimate` on the gxt 929. use a fingertip beside the mouse nose, slide forward until the rear edge reaches it, and repeat three times. confirm the displayed value is labeled as an estimate with pass spread. escape must cancel without overwriting history. other mice use `calibrate <cm>` with a known distance.
11. compare status for the trust receiver and another selected mouse. only matching hid collections should appear; trust model facts should not be attached to an unrelated device.

physical movement and distance calibration are not covered by the automated tests.

a historical sustained-motion result exists for this receiver, but it does not establish its configured polling rate. physical dpi accuracy, actual transformed hand movement and in-game camera response remain manual checks. see [aim verification](aim.md).
