# aim tools / research notes

checked 2026-10-06. presets change mouse input, not game state or targets. no claim of guaranteed score improvement.

## refinement / 2026-10-07

reviewed the [official v1.7.1 coalescion guide](https://github.com/RawAccelOfficial/rawaccel/blob/v1.7.1/doc/Guide.md#coalescion), [processing implementation](https://github.com/RawAccelOfficial/rawaccel/blob/v1.7.1/common/rawaccel.hpp), [jitter discussion](https://github.com/RawAccelOfficial/rawaccel/issues/119), and [community polling discussion](https://www.reddit.com/r/MouseAccel/comments/1d2l8pz/raw_accel_is_limiting_my_mouse_polling_rate/). forum comments supplied questions to investigate; implementation follows the released engine and is tested against it.

smooth now offers 2/4/8 ms choices and an advanced 1..12 ms integer range. the range and presets are helox choices, not upstream recommendations. tests run the official engine at 8 ms report intervals with alternating 40/120-count motion, check that larger half-lives reduce alternating output spread, and check direction/sign preservation. this is an algorithm check, not a measurement of physical latency or aim performance. custom strength persists across off/on, precision changes, and resume; older boolean-only presets retain 4 ms.

delivery tests now expose span frequency and gap time beside active frequency, because excluding pauses can make the headline look healthy despite interruptions. the slow-interval heuristic is based on the observed median, with a 0.25 ms margin for small timestamp variation. the former `max(12 ms, 3 * median)` threshold missed 16 ms intervals on an otherwise 125 hz stream. synthetic tests reproduce that case. neither timing statistic reads hardware polling or identifies whether a pause was radio loss.

## sources and decisions

- [official raw accel guide](https://github.com/RawAccelOfficial/rawaccel/blob/v1.7.1/doc/Guide.md): natural gain, whole-vector application, and input/sensitivity/output ema smoothing. output smoothing adds delay; input smoothing alone does not smooth the movement sent to a game.
- [official wrapper](https://github.com/RawAccelOfficial/rawaccel/blob/v1.7.1/wrapper/wrapper.cpp) and [processing algorithm](https://github.com/RawAccelOfficial/rawaccel/blob/v1.7.1/common/rawaccel.hpp): we use the released bridge for configuration validation, activation and live readback. whole-vector output smoothing averages magnitude and keeps direction. the preset uses no snapping, axis restriction, rotation, dpi normalization or polling override.
- [mouse sensitivity community](https://www.mouse-sensitivity.com/forums/topic/7925-rawaccel-method-to-convert-static-sensitivity-to-acceleration-curve/), [r/mouseaccel discussion](https://www.reddit.com/r/MouseAccel/comments/y59gew/), [r/fpsaimtrainer discussion](https://www.reddit.com/r/FPSAimTrainer/comments/zqnxbx/): useful experiences with slower corrections and faster turns. these are personal reports, not proof of a universal best curve. the conservative 1.4x cap and other preset numbers are helox implementation choices, not manufacturer recommendations.
- [jitter feature discussion](https://github.com/RawAccelOfficial/rawaccel/issues/119): historical demand for smoothing; the actual implementation is verified against the current source instead of treating an old issue as an available feature.
- [intel usb 3 interference measurements](https://www.benq.com/content/dam/newb2b/Support/FAQ/intel_white%20paper_usb3%20interference.pdf), [receiver placement experiences](https://www.reddit.com/r/MouseReview/comments/zgbrk3/): receiver placement has physical evidence. registry/timer/controller deletion suggestions from forum comments were not implemented.

## verification

delivery comparison refinement on 2026-10-07 also reviewed [AleksMouseTester's comparison methodology](https://github.com/moneks1106-code/AleksMouseTester#what-it-does) and [ClickSync's measurement manual](https://github.com/Nuitfanee/ClickSyncMouseTester/blob/main/docs/ClickSyncMouseTester_User_Manual_EN.md). these are project descriptions, not independent validation of helox. helox now compares tail intervals and gap/slow shares, normalizes outlier counts by the number of delivered intervals, and flags batching or pauses. it does not rank runs or infer a better physical polling configuration from those deltas. unknown dpi and test conditions remain a reason to repeat similar movement.

the official v1.7.1 zip sha256 is `770fe3ae0919ca3c4d412f58c985eb27f5434decad809f7e8206de4e8852eec4`. its wrapper reports protocol version 1.7.0; this is the upstream release content, not a helox version mismatch. driver authenticode status was valid, signer microsoft windows hardware compatibility publisher.

run `aim prepare`, then `selftest`. tests execute the official native calculation engine without installing the driver: 1x slow motion, increased fast motion within the 1.4x bound, reduced alternating magnitude variation, preserved signs and direction, valid combined configuration and repeated-update scoping. no kernel write occurs in these tests.

live application requires installation and restart. the current machine had no installed raw accel driver during development, so kernel application and physical before/after stutter reduction remain manual checks. absent-driver activation is tested to fail rather than claim success. apply reads back the full configuration and attempts rollback on a mismatch. the driver backup is validated before further changes.

for a physical comparison, run `3` while continuously moving in circles, enable only precision or smooth, repeat the same movement and test tracking/flick tasks in kovaaks. compare p95/p99, long gaps and feel. delivery timing does not measure smoothing quality or end-to-end latency. expect radio gaps to need receiver/surface/power troubleshooting rather than a stronger ema.

the complete original driver snapshot is restored by `aim restore`; it also restores its original device overrides. the snapshot is retained across sessions. no automatic startup application or reboot is performed. driver transforms cannot recover missing reports, and output smoothing does not remove directional hand tremor.

## saved presets

after a verified precision/smooth change, helox atomically stores both choices by hardware id in `aim-presets.json`. `aim resume` or menu `8 > 8 resume saved` applies these choices in one driver update, preserving other devices. it requires the driver to be installed and active, and never runs automatically when opening the app. existing users need one successful aim change in 0.5.0 to create a saved preset.

malformed preset files block changes before activation. a persistence failure triggers driver rollback. `aim restore` clears saved helox choices after restoring the original driver snapshot; a later resume cannot silently reinstate an undone preset.
