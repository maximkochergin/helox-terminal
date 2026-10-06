# verification

## automated on windows

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

## manual pass

1. double-click `launch.bat`. confirm white lowercase interface, actual mouse product name and live settings.
2. run `profile save before`. run `setup`. confirm acceleration is on, existing thresholds/mode are preserved, and the desktop cursor response changes where applicable.
3. change `set speed 6`, then `set speed 15`. compare desktop response. raw input games should retain their own sensitivity.
4. run `restore`. confirm settings equal the original snapshot. use `profile apply before` if testing from a later already-customized session.
5. run `measure 10` three times while moving continuously. compare average observed delivery, not a supposed configured hardware value. json includes median hz as a separate diagnostic.
6. run `calibrate 10` three times against ruler marks. compare estimates. press the physical dpi button, recalibrate, and compare the new estimate; the saved history is never automatic hardware readback.
7. power the mouse off while leaving the receiver attached. status may still enumerate the receiver; a capture without motion should fail without saving a result.
8. select the trust device and disconnect its receiver. measurements must report the missing selected device, without switching to the touchpad.
9. in the legacy console, click/drag during a test. capture should keep running; the original quick-edit mode should return afterward.

physical movement and distance calibration are not covered by the automated tests.

a live 15-second capture was attempted during development and rejected for insufficient motion reports. no frequency value was saved or claimed as a physical measurement. a successful sustained-motion run and ruler calibration remain unverified.
