# game presets

open `1 game setup`, choose your game, then `1 apply`, `2 change style`, `3 preview checks` or `4 in-game checklist`. kovaak's opens a second choice: match valorant, match cs2 or tracking practice. `5 undo or recover` in the game menu restores the previous windows settings, complete driver configuration and saved aim choices together. profiles save windows settings; game recipes specify the complete selected-mouse processing chain.

each game has three styles. `balanced` uses the table below. `steady` keeps settled slow sensitivity at 1x, disables micro damping, enables 4 ms scale averaging and lowers the fast-turn limit to 1.15x for valorant, 1.2x for cs2 or 1.25x for tracking practice. `linear` disables stability and damping and uses a constant 1x table: a reference for comparison with the same game sensitivity. matched training recipes use identical controls for the same style.

`preset tune` or `game setup > 4` records two eight-second captures: continuous slow corrections and normal fast sweeps. input must be untransformed; bypass effects first through `tune mouse > driver > bypass`. the tool does not change or bypass settings automatically. the personalized ramp starts at 110% of the slow phase's 90th-percentile speed and reaches the existing bounded limit at the fast phase's 75th-percentile speed. short, batched, poorly separated or stale captures are rejected. tuning is tied to the selected device and expires after seven days. repeat after changing the physical dpi stage; the tool cannot detect that stage. the measured interval describes raw delivery, not configured polling. no game process is read.

choose `5 built-in or personal speed range` in the recipe screen, or append `personal` to a command. this changes ramp speeds, keeps the style's limits and preserves matched game/trainer processing. `linear` does not use a speed range. a personalized last-applied recipe is checked against its saved full readback; unrelated mouse selections do not inherit its label.

apply also requires a confirmed started raw accel stack with no device problem for the selected mouse; an accessible global driver endpoint alone is insufficient. preview reports stack state and remains read-only.

preview and apply run the official engine model at 8, 4, 2 and 1 ms intervals. the report checks the flick tail and reversal direction; apply stops before mutation if these checks fail. these are example rates, not measurements of your mouse or game. window/driver application and combined undo retain their durable recovery journal.

the setup screen shows the style, speed range, enabled filters and settings kept unchanged; the in-game checklist stays separate. `preset show` explains each component decision. preview returns to the same recipe so you can apply without starting again. the game menu and `preset status` compare the selected device's effective profile and configuration against the complete recipes, including the enabled flag, normalization and timing. profile names and unrelated mice do not affect matching. windows matching is reported separately; game files are not inspected. matched training recipes share the same settings, so two names can appear together. a custom setup or a driver reset after reboot does not get an active-preset label just because choices were saved. the menu refuses to apply a preview to a replacement mouse after disconnection.

## engine and game settings

valorant moved to unreal engine 5.3 in [riot's 11.02 notes](https://playvalorant.com/en-us/news/game-updates/valorant-patch-notes-11-02/). [11.06](https://playvalorant.com/en-us/news/game-updates/valorant-patch-notes-11-06/) made raw input buffer permanently enabled. no obsolete buffer toggle is required. windows pointer acceleration does not set the in-game acceleration curve; the selected raw accel device is configured upstream.

cs2 uses [source 2](https://store.steampowered.com/app/730/CounterStrike_2/). this recipe does not copy `m_rawinput` or `m_customaccel` commands from cs:go guides. keep your actual in-game sensitivity and physical dpi button stage. compare in offline practice; [valve's frame-time telemetry](https://help.steampowered.com/en/faqs/view/5E6F-5B36-5485-F6B9) helps distinguish rendering interruption from input processing.

kovaak's has [game-specific sensitivity and fov scales](https://wiki.kovaaks.com/en/home/KovaaK%27s/Settings). select the target game's scales and match your sensitivity, dpi stage and aspect ratio. the same fov number across engines need not mean the same view; [the fov conventions](https://www.kovaak.com/film-notation/) explain why. `kovaaks-valorant` and `kovaaks-cs2` use **exactly the target recipe's driver controls**, so practice does not silently add another filter. the separate tracking variant uses the valorant scales and a different filter combination.

these are researched integration choices. the numeric curves below are conservative helox starting points, not values recommended by riot, valve or kovaak's. an engine name alone cannot determine a personal acceleration curve. unknown physical dpi prevents a supported cm/360 or hand-speed recommendation. changing the dpi button stage changes how these count-based curves feel.

## complete tool settings

| recipe | base | ramp, counts/ms | fast/base | shape | stability | micro damping |
| --- | --- | --- | --- | --- | --- | --- |
| valorant | 1x | 3..35 | 1.2x | 1.5 | off | off |
| cs2 | 1x | 4..45 | 1.25x | 1.5 | off | off |
| kovaaks-valorant | 1x | 3..35 | 1.2x | 1.5 | off | off |
| kovaaks-cs2 | 1x | 4..45 | 1.25x | 1.5 | off | off |
| kovaaks-tracking | 1x | 2..30 | 1.35x | 1 | scale half-life 4 ms | 0.9x -> 1x at 0.75 counts/ms |

every recipe enables precision and the selected device, sets the personal sensitivity LUT, disables input-speed and output-magnitude averaging, disables angle snapping and resets all direction scales to 1x. micro damping reduces all slow motion, including intended corrections; the tactical recipes leave it off. prediction of future motion or unwanted hand intent is unavailable in the signed backend and is not represented as an active setting.

windows pointer speed becomes 10/20 and desktop acceleration becomes off. wheel, buttons, double-click timing and remembered thresholds are retained. device **software** dpi normalization and polling override become zero, constant interval becomes off and time bounds become the upstream defaults, 0.0625..100 ms. no hardware dpi or usb polling setting is written. defaults, peer mice and shared profiles are preserved. remembered inactive strengths are saved too, so subsequent controls and resume have a complete definition.

apply verifies windows readback and the complete native driver readback after its activation delay. failure recovers changed native components and saved files independently; a failure before driver activation leaves the driver untouched. repeating the same applied recipe preserves useful undo and avoids another native activation. undo stops if either native state or saved choices changed afterward, preserving newer edits. the original windows and aim backups remain separate.

from 0.14.1, both apply and undo save `game-recovery.json` **before** changing windows, the driver or saved choices. closing/killing the process or a failed rollback retains this complete checkpoint. opening helox reports pending recovery; `1 game setup > 5` becomes `recover interrupted preset`, also available as `preset recover`. close games first. windows and aim changes, new recipes and live verification are blocked until recovery finishes. recovery restores the full driver state, windows values and exact previous bytes/absence of `aim-presets.json`, `game-undo.json` and `undo.json`; the checkpoint is removed only after every component succeeds.

recovery explicitly writes and verifies the original driver configuration because the outcome of an interrupted write is uncertain. it attempts the other components independently if the driver cannot be restored and retains the checkpoint for retry. malformed prior undo or saved-controls snapshots fail preflight before settings change. the checkpoint covers process interruption; it does not guarantee durability after power loss or disk failure.

the released backend's one-second delay happens inside synchronous `Activate` / `DeviceIoControl`, before the call returns. immediate readback is therefore sufficient after a successful return; helox no longer adds another 1.2-second wait. real kernel write/readback tests cover this behavior. [released I/O implementation](https://github.com/RawAccelOfficial/rawaccel/blob/v1.7.1/common/rawaccel-io.hpp) / [driver delay](https://github.com/RawAccelOfficial/rawaccel/blob/v1.7.1/driver/driver.cpp).

**in-game settings are manual.** the recipe screen lists the required sensitivity/fov choices; no game configuration file is edited and JSON reports `GameSettingsApplied: false`. do not copy a guessed dpi value into a sensitivity converter. after reboot, `aim resume` restores the saved driver controls; it does not change windows settings.

```text
preset list
preset show valorant
preset preview cs2
preset preview cs2 steady
preset preview valorant linear
preset tune
preset preview valorant steady personal
preset apply kovaaks-valorant
preset undo
preset recover
```

## live verification

`3 test mouse > 5 live verification` or `aim verify` requires the supported games to be closed and a confirmed started raw accel stack for the selected mouse. it temporarily activates 13 cases: precision, output smoothing, stability, personal curve, snapping, directions, micro damping, bypass and all five game recipes. each activation is compared with full delayed kernel readback. the original complete driver state is restored before a three-second passive raw-input capture. saved presets and windows preferences are not changed by this diagnostic.

the snapshot is saved before the first write. if verification is interrupted, `aim verify restore` restores it; aim and recipe writes are blocked until recovery finishes. status shows the pending recovery and live verification menu action becomes `restore live verify`. closing the terminal does not run restoration code, so restore after an interrupted run. verification ratios come from the released native engine applied to the actual readback. `PhysicalTransformVerified` and `GameInputVerified` remain null: kernel configuration and input registration are verified, actual hand-motion transformation and the game's camera response need a physical comparison. no synthetic input is injected.

for a game comparison, keep sensitivity and the dpi button stage fixed, use the matching training recipe and compare slow corrections with fast turns. the tactical recipes intentionally preserve 1x settled slow sensitivity, so a small correction alone will not demonstrate the acceleration ramp. [response diagnostics](manual.md#aim-tools) show which example speeds reach it.
