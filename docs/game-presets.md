# game presets

open `9 game presets`, choose a recipe, then `1 preview` or `2 apply`. `6 undo last game preset` restores the previous windows settings, complete driver configuration and saved aim choices together. a named profile remains your saved windows configuration; these built-in recipes specify the complete selected-mouse processing chain.

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

apply verifies windows readback and the complete native driver readback after its activation delay. failure attempts driver, windows and file recovery independently. repeating the same applied recipe preserves useful undo and avoids another native activation. undo stops if either native state or saved choices changed afterward, preserving newer edits. the original windows and aim backups remain separate.

**in-game settings are manual.** the recipe screen lists the required sensitivity/fov choices; no game configuration file is edited and JSON reports `GameSettingsApplied: false`. do not copy a guessed dpi value into a sensitivity converter. after reboot, `aim resume` restores the saved driver controls; it does not change windows settings.

```text
preset list
preset show valorant
preset preview cs2
preset apply kovaaks-valorant
preset undo
```

## live verification

`8 aim tools > 21 live verify` or `aim verify` requires the supported games to be closed and a confirmed started raw accel stack for the selected mouse. it temporarily activates 13 cases: precision, output smoothing, stability, personal curve, snapping, directions, micro damping, bypass and all five game recipes. each activation is compared with full delayed kernel readback. the original complete driver state is restored before a three-second passive raw-input capture. saved presets and windows preferences are not changed by this diagnostic.

the snapshot is saved before the first write. if verification is interrupted, `aim verify restore` restores it; aim and recipe writes are blocked until recovery finishes. status shows the pending recovery and aim menu item 21 becomes `restore live verify`. closing the terminal does not run restoration code, so restore after an interrupted run. verification ratios come from the released native engine applied to the actual readback. `PhysicalTransformVerified` and `GameInputVerified` remain null: kernel configuration and input registration are verified, actual hand-motion transformation and the game's camera response need a physical comparison. no synthetic input is injected.

for a game comparison, keep sensitivity and the dpi button stage fixed, use the matching training recipe and compare slow corrections with fast turns. the tactical recipes intentionally preserve 1x settled slow sensitivity, so a small correction alone will not demonstrate the acceleration ramp. [response diagnostics](manual.md#aim-tools) show which example speeds reach it.
