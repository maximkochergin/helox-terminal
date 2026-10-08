# personal curves

open `8 aim tools > 15 curve builder`. the draft does not change the mouse until you choose `7 apply`.

| control | meaning | range |
| --- | --- | --- |
| base | sensitivity below the start speed | 0.25..2x |
| start | speed where acceleration begins | 0..1000 |
| end | speed where the fast plateau begins | greater than start by at least 0.1; max 2000 |
| fast / base | fast sensitivity divided by base sensitivity | 1..3x |
| shape | moves the transition earlier below 1, or later above 1 | 0.5..3 |

fast sensitivity is `base * limit`. for example, base `0.8` and limit `1.8` give slow `0.8x` and fast `1.44x`. the ranges are helox engineering limits, not riot approval thresholds.

speed units are `counts/ms` when the selected device's raw accel dpi normalization is zero, or `in/s` when a positive normalization dpi is configured. helox preserves that device setting, timing and bypass options. no physical dpi is guessed. changing the physical dpi stage changes the curve's relationship to hand speed unless normalization is also correctly recalibrated.

`6 preview` calculates eight example report sizes with the official engine and your current filters. the proposed curve is enabled in the model even if the device is currently bypassed, matching apply. no driver activation, backup or preference write occurs. the interval uses recent matching delivery history or a labeled 8 ms example. ratios include configured dpi normalization; examples are not an in-game or physical latency measurement.

the curve is sampled into a native sensitivity table. a quintic smoothstep of the shaped transition joins constant slow and fast plateaus. there are 128 transition segments plus plateau points, rounded to native floats and validated by the official bridge. the [released lookup implementation](https://github.com/RawAccelOfficial/rawaccel/blob/v1.7.1/common/accel-lookup.hpp) extrapolates its final segment. helox appends a constant tail so motion beyond the end stays at the plateau. native tests cover interpolation and far-above-end speeds.

apply uses the existing serialized transaction, original snapshot, delayed readback and rollback. only the selected device override and its private profile change. curve parameters and optional snapping are saved by hardware id for `aim resume`. smooth, stability, tracking and plain precision off/on preserve the curve. an explicit natural gain choice, `8 natural curve`, or `aim curve natural` switches to natural acceleration. the draft stays editable after either apply action; leaving discards unapplied edits.

if another app modifies an active helox lookup table, its interpretation or processing layout, component switches stop instead of rebuilding from stale saved parameters. apply a new curve or resume the saved preset explicitly; `aim bypass on` disables the selected mouse's effects even with damaged saved controls. legacy presets keep natural acceleration and snapping off. the builder refuses a different mouse or changed speed units between editing and applying; apply rechecks units inside the settings transaction. [independent filters](filters.md) also cover direction attenuation and micro damping.

```text
aim curve
aim curve preview 1 3 30 1.4 1
aim curve apply 1 3 30 1.4 1
aim curve natural
aim resume
```

these values demonstrate syntax; they are not a personalized recommendation.

## angle snapping

`8 > 16` controls the existing signed driver's axis snapping. it defaults off. menu choices offer off, 1 degree, 2 degrees and a custom angle within 0..5 degrees. commands: `aim snap on [degrees]` and `aim snap off`; omitting the angle retains the last chosen nonzero strength, with 1 degree as the initial fallback. off/on no longer resets a chosen angle.

the [official implementation](https://github.com/RawAccelOfficial/rawaccel/blob/v1.7.1/common/rawaccel.hpp) snaps near-horizontal or near-vertical input to that axis while retaining magnitude and sign. diagonal motion outside the threshold is unaffected. it uses mouse input only, with no game, target, screen or weapon information. unlike acceleration, it changes direction and can suppress intended small corrections near an axis. precision off does not disable this separate filter; use snap off.

five degrees is a helox limit, not an established permissible level for vanguard. no public numeric approval threshold was found in [riot's third-party policy](https://support.riotgames.com/en-us/riot/events/third-party-applications). the setting is optional and not riot certified. anti-cheat compatibility of raw accel does not establish approval of every configuration or tournament use.

## shutdown popup

`6 more > 7`, `8 aim tools > 17`, or `aim events --json` reads the windows journals without changing settings. it lists recent application crashes and whether they occurred within two minutes of a shutdown request. times are utc. coverage is the last 14 days, at most 128 application errors and 64 shutdown requests; scan limits and access errors are reported.

the report covers application error event 1000 and user32 shutdown request event 1074. it does not rule out unlogged popups, .net-runtime-only events, hangs or kernel failures. unmatched timing stays unknown if the shutdown journal is unavailable or its scan is incomplete. timing proximity is not proof of the cause. filenames are reported without full executable paths or account details. no third-party program is removed or disabled.

raw accel profiles normally reset on reboot. `aim resume` reapplies saved choices; opening the terminal does not automatically apply them.
