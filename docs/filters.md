# input filters

open `2 tune mouse > 2 motion filters`. new filters start off; opening this release does not apply them. these controls configure the unchanged, signed raw accel 1.7.1 driver. they use mouse input only. there is no screen capture, game process access, target detection or input injection.

| menu | control | effect |
| --- | --- | --- |
| 16 | angle snapping | redirects input close to a horizontal or vertical axis |
| 18 | direction scales | independently attenuates left, right, up and down |
| 19 | micro damping | reduces sensitivity at low speed and smoothly recovers |
| 20 | bypass all | disables every raw accel effect for the selected mouse |

## direction scales

`1x` keeps a direction; `0.25..1x` attenuates it. the menu offers neutral, vertical `0.85x`, or four custom scales. custom choices have preview and apply steps. these are software output multipliers, not physical dpi settings. there is no complete direction lock, reversal or automatic per-game change.

the [official implementation](https://github.com/RawAccelOfficial/rawaccel/blob/v1.7.1/common/rawaccel.hpp) uses output dpi and vertical/left/up ratios. helox converts four independent scales into those fields and reads the resulting scales back. device dpi normalization, polling and timing remain separate. unequal horizontal and vertical scales also change diagonal angles.

```text
aim directions preview 1 1 0.85 0.85
aim directions apply 1 1 0.85 0.85
aim directions off
```

## micro damping

precision or a personal curve must be enabled first. below the recovery speed, a cubic smoothstep increases the multiplier from the chosen low scale to `1x`. at and above recovery the multiplier is `1x`. ranges: low scale `0.25..1x`, recovery `0.1..20` in the profile's displayed speed unit (`counts/ms` or `in/s`). light/balanced menu choices are `0.85x`/`0.75x` with recovery `1`; these are helox choices, not verified settings for your hand or dpi stage.

this reduces **all** slow movement, including intended corrections. it does not infer hand intent or identify unwanted motion. there is no hard deadzone or extra trajectory averaging. LUT input-speed averaging is disabled; optional stability averages the sensitivity coefficient instead. fractional counts use the driver's existing carry, so very small motion can appear less frequent even though counts accumulate. this cannot repair radio gaps or missing reports.

the multiplier is composed with a personal sensitivity table. above recovery, that original table is retained within native float rounding. for natural acceleration it uses a sampled version of the [released natural gain formula](https://github.com/RawAccelOfficial/rawaccel/blob/v1.7.1/common/accel-natural.hpp). natural sampling is an approximation; native regression samples bound the error to `0.001x`. its final tail is flat above about `32771` speed units. damping off returns to the native natural calculation, or to the unchanged personal curve. the [native lookup](https://github.com/RawAccelOfficial/rawaccel/blob/v1.7.1/common/accel-lookup.hpp) is limited to 257 points; generated combined tables fit that limit and have a flat final segment.

```text
aim damp preview 0.85 1
aim damp on 0.85 1
aim damp off
```

precision off bypasses this curve component and remembers its choices. snapping and direction scales remain independent. tracking retains these optional choices and disables output averaging; it is not a reset-all command. `aim curve natural` changes the base curve and retains damping when units match. `aim damp off` removes damping separately. `aim resume` restores saved controls, including remembered snap strength, after reboot. speed-unit changes block reuse of stored custom speeds; explicitly rebuild a compatible curve or use bypass. an explicit curve rebuild in a new unit resets the incompatible micro controls to their off defaults, rather than reinterpreting the old recovery threshold.

## snapping, prediction and interaction

snapping defaults off and uses the existing native axis filter, limited by helox to `0..5` degrees. `aim snap on` retains the last chosen nonzero angle; a legacy preset starts at 1 degree. see [snapping behavior](curves.md#angle-snapping).

native processing rotates/snaps the input, computes acceleration and optional output magnitude smoothing, then applies dpi/direction scales. therefore the snapping threshold describes the input angle, while asymmetric scales can change the final angle. `aim response --json` includes fresh-state cardinal, diagonal and near-axis examples alongside horizontal speed samples and the integer-carry flick recovery example. preview marks the profile as proposed and never activates it.

the signed backend has no future-trajectory prediction control. its EMA implementation includes a **speed** trend estimate; this is not prediction of the direction or endpoint of your hand movement. helox does not substitute a different kernel driver or synthetic input for that missing feature.

## recovery and compatibility

`aim bypass on` disables processing only for the selected hardware id, without reading saved presets or requiring a valid original backup. it preserves profiles, peer devices, calibration and timing. `aim bypass off` enables the current profile; it does not load a saved preset. bypass is temporary driver state and does not rewrite the saved choice. changing an effect to on or resuming also explicitly enables processing. use `aim restore` for the separate original snapshot across all devices.

externally changed lookup data, interpretation, rotation, domain/range weights, norm, component mode or input speed cap block ordinary component rebuilding from stale parameters. bypass still works for an inspectable valid configuration. an unreadable driver or rejected activation is reported, with the existing rollback path; bypass cannot be guaranteed when the driver itself cannot be read or written.

tests cover signed-engine calculations and combinations, not an actual valorant session or vanguard approval. [riot's third-party policy](https://support.riotgames.com/en-us/riot/events/third-party-applications) does not publish a numeric approval threshold for these settings. a working signed raw accel installation does not certify every filter configuration. optional direction-changing controls remain off until explicitly applied.
