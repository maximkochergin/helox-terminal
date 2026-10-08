# verification

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
10. run menu item 4. use a fingertip beside the mouse nose, slide forward until the rear edge reaches it, and repeat three times. confirm the displayed value is labeled as an estimate with pass spread. escape must cancel without overwriting history.
11. compare status for the trust receiver and another selected mouse. only matching hid collections should appear; trust model facts should not be attached to an unrelated device.

physical movement and distance calibration are not covered by the automated tests.

a historical sustained-motion result exists for this receiver, but it does not establish its configured polling rate. physical dpi accuracy and the new driver's effect on this mouse remain manual checks. see [aim verification](aim.md).
