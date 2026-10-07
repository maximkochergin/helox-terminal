# configuration audits

## 0.10.1 / active aim controls

an effect switch rebuilt the selected device configuration from factory defaults. a synthetic bypassed mouse with dpi normalization 1600, constant 125 hz timing and custom interval bounds became enabled with normalization and timing reset after `smooth off`. preservation checks failed on the previous implementation. switches now copy the effective device configuration, including inherited defaults; off retains bypass, while an explicit on, tracking or resume enables the device. only the enable flag changes for that request. these are calculation settings, not sensor dpi or usb polling writes.

status and response also matched device ids without regard to case, but the [released callback](https://github.com/RawAccelOfficial/rawaccel/blob/v1.7.1/driver/driver.cpp) matches them exactly with `wcsncmp`. an override named `hid\first` was therefore reported as active for `HID\FIRST` even though the kernel would use its default. a failing regression reproduced the lookup mismatch without activation. status and response now share the exact matcher; saved preference lookup remains case-insensitive, and applying a choice writes the currently enumerated id.

repeated aim undo previously always activated the snapshot, including when it already matched. undo now uses the same verified no-change commit path as switches, clears saved choices and skips the write that would reset smoothing state. persistence failures still roll back a changed configuration; an unchanged save failure does not touch the driver.

checks cover per-device and inherited calibration/timing, all off switches, explicit enable, source snapshot immutability, exact-id fallback and unchanged restore commits. native response cases verify preserved 0.625x normalization after a smoothing toggle, bypass after an off request, and default timing/profile when an id differs only in case. current live configuration is read and compared before/after the review; no aim settings are activated by these tests.

## 0.8.1 / maintenance review

the 0.8.0 driver doctor reported failed backend verification but still attempted kernel readback, which loaded the bridge. readback now stays unknown when preparation/archive/backend verification fails. every first bridge load also checks integrity, so opening the aim menu cannot bypass the diagnostic guard. tests use a temporary app with a deliberately failed integrity report; no vendor binaries are changed.

reset previously checked delete access only after restoration and uninstall. a locked backend could therefore abort cleanup after settings or driver state had already changed. delete-access checks now run before any mutation and repeat before deletion. all snapshots that will be restored are validated before restoration starts, preventing a damaged aim snapshot from aborting only after the windows snapshot has been applied. a malformed aim backup is irrelevant when the driver is confirmed unloaded.

startup checks also ran before json argument parsing, so blocked json commands emitted plain text. errors now retain the requested json format. regressions cover the blocked session, unverified bridge paths, locked preflight, invalid backups and reset callback ordering. uninstall failure/pending-deletion tests were rerun with mocked system operations; no real uninstall or purge was performed.

## 0.7.2 / ambiguous json fields

two load paths checked case-sensitive dictionary keys, then passed the same input to a case-insensitive deserializer. a windows snapshot with `"Speed":10` and `"speed":"20"` passed integer checks and loaded speed 20. the official aim bridge accepted a profile with `"Output DPI":1000` and `"output dpi":2000`, producing output dpi 2000 after the guard had checked 1000. both mismatches were reproduced without applying settings or activating the driver.

windows snapshots now reject case aliases for settings fields and construct the result directly from the validated integers. aim configuration rejects case-colliding keys at every nesting level, before bridge conversion. optional device fields must also use their canonical spelling, so aliases cannot bypass finite-number, boolean or interval checks when the canonical field is absent.

regressions cover both snapshot field orders, root/profile/vector/acceleration aliases, optional device aliases and valid canonical optional values. these are configuration-validation bypasses; this audit does not claim a demonstrated kernel crash. ordinary saved snapshots and official default configurations remain compatible.

## 0.5.1 / driver configuration

## reproduced

using the official 1.7.1 bridge without activating the driver, conversion accepted a string `NaN` in output dpi, a 256-character profile name and duplicate profile names. the bridge coerces numeric strings and marshals names into fixed native buffers. relying on its validation alone allowed malformed restore files to reach native code, with invalid output calculations or ambiguous/truncated identities.

helox now checks the managed configuration before conversion: mandatory types, finite numbers, utf-16 name/id limits, embedded nul, unique profiles and device ids, valid references, time clamps and integer fields. lookup values must fit the native float representation. malformed data never reaches bridge conversion or activation. the official validator still checks its mathematical constraints afterward.

sixteen malformed fixtures run in `check` and github actions. the valid fixture is generated from `DriverConfig.GetDefault().ToJSON()` in the official release; upstream is mit licensed. official native engine tests confirm compatibility with valid helox presets. lookup speeds must increase after conversion to native floats, preventing duplicate interpolation positions and float rounding collisions.

## additional fixes

the service registry alone cannot prove that a filter is absent: an uninstalled driver may stay loaded until restart. status now probes the same control endpoint used by the bridge; access errors stay unknown. this prevents dpi estimation from treating an unreadable loaded filter as unfiltered. the decision is covered by logical regression cases; a loaded-driver uninstall/restart sequence remains a manual check.

the installer now checks the official process exit code before accepting filesystem/service checks, so an old installation cannot mask a newly failed installer process.

no invalid configuration was activated during this audit. tests cover rejection and calculation behavior, not a claimed crash demonstration or physical improvement on an installed driver.
