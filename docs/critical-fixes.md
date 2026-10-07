# configuration audits

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
