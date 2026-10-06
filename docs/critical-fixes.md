# driver configuration audit / 0.5.1

## reproduced

using the official 1.7.1 bridge without activating the driver, conversion accepted a string `NaN` in output dpi, a 256-character profile name and duplicate profile names. the bridge coerces numeric strings and marshals names into fixed native buffers. relying on its validation alone allowed malformed restore files to reach native code, with invalid output calculations or ambiguous/truncated identities.

helox now checks the managed configuration before conversion: mandatory types, finite numbers, utf-16 name/id limits, embedded nul, unique profiles and device ids, valid references, time clamps and integer fields. lookup values must fit the native float representation. malformed data never reaches bridge conversion or activation. the official validator still checks its mathematical constraints afterward.

fourteen malformed fixtures run in `check` and github actions. the valid fixture is generated from `DriverConfig.GetDefault().ToJSON()` in the official release; upstream is mit licensed. official native engine tests confirm compatibility with valid helox presets.

## additional fixes

the service registry alone cannot prove that a filter is absent: an uninstalled driver may stay loaded until restart. status now probes the same control endpoint used by the bridge; access errors stay unknown. this prevents dpi estimation from treating an unreadable loaded filter as unfiltered. the decision is covered by logical regression cases; a loaded-driver uninstall/restart sequence remains a manual check.

the installer now checks the official process exit code before accepting filesystem/service checks, so an old installation cannot mask a newly failed installer process.

no invalid configuration was activated during this audit. tests cover rejection and calculation behavior, not a claimed crash demonstration or physical improvement on an installed driver.
