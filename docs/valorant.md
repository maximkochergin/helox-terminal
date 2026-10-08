# valorant / input and compatibility

checked 2026-10-08.

## why a change can feel small

riot confirms [raw input since launch](https://playvalorant.com/en-us/news/game-updates/valorant-patch-notes-3-07/); [11.06](https://playvalorant.com/en-us/news/game-updates/valorant-patch-notes-11-06/) made raw input buffer always enabled. windows pointer acceleration controls the desktop path, so it is not a valorant acceleration control. helox's home now displays desktop settings and the aim profile separately.

the inspected profile is enabled natural 1.4x, input/scale half-lives 4/2 ms, output half-life 8 ms. windows reports rawaccel in the selected receiver's stack, alongside mouclass, mouhid and hidusb. this supports correct installation/scoping; it does not measure counts received during a match.

at approximately 8 ms intervals, the official-engine examples give 1.000x for 24 counts per report, 1.093x for 80 and 1.199x for 160. the limit is approached gradually. smaller motion may therefore remain near 1x; this is a plausible explanation for a subtle effect, not a measurement of the user's hand speeds. output averaging also retains magnitude from earlier motion and can distort a small turn after a fast burst. response shows that transient separately. neither fact proves an anti-cheat block or diagnoses rendering/radio pauses.

## compatible design

helox configures the verified, signed official raw accel driver and keeps its delayed update mechanism. its [authors report valorant compatibility](https://github.com/RawAccelOfficial/rawaccel/blob/master/doc/FAQ.md), while explicitly declining an absolute anti-cheat guarantee. the terminal does not access game memory, inject code, automate aiming or alter vanguard/security settings. applying a profile does not require keeping the terminal open.

[riot's third-party policy](https://support.riotgames.com/en-us/riot/events/third-party-applications) restricts unfair advantages and gameplay automation. a signature or successful local check is not riot approval. if vanguard reports an [incompatible component](https://support.riotgames.com/en-us/riot/performance/error-van-incompatible-software), retain the exact named file/error and follow riot support guidance; helox does not hide or bypass that block.

## manual comparison

1. run `aim doctor` and `aim response` before play. record the current gain, smoothing and stability values.
2. use the practice range with the same physical dpi stage and game sensitivity. compare repeat slow and fast strokes over similar travel, including small corrections after a flick. opening the terminal applies nothing.
3. test one component at a time. `aim smooth off` leaves precision/stability in place; restore the recorded output strength with `aim smooth on <ms>`. this isolates output averaging more clearly than changing several filters at once.
4. recheck `aim status` after a restart or unexpected change. driver choices reset on reboot; `aim resume` reapplies the most recently saved choices, including any off switches used during testing. it is not an undo to an earlier test state.

normal game startup is evidence against an explicit launch-time rejection, not proof that every transformed report reached the game. verifying physical feel and in-game consumption remains a manual pass.
