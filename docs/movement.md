# flicks, corrections and sensor tracking

## start with the processing chain

`aim status` reads the active driver. `aim response` tests that configuration with example motion in the released calculation engine. the flick-to-turn and flick-to-reverse cases include integer carry; these are simulations, not measurements of your hand or proof of in-game improvement.

output averaging can carry the magnitude of a fast flick into a tiny correction. `8 aim tools > 22 remove flick tail` (or `aim smooth off`) disables that averaging and saves the choice. it keeps the selected curve, physical dpi and game sensitivity. other filters remain configured; use `aim response` to inspect their combined result. after a reboot, `8 > 8 resume saved` restores saved aim choices.

the valorant and cs2 recipes also disable output averaging. their acceleration starts at 1x for settled slow movement and has a bounded fast-motion gain. they intentionally disable snapping, direction reduction and micro damping: those filters alter intended corrections too. kovaak's matched recipes use the same driver settings as their target game. the separate tracking recipe deliberately adds mild micro damping and is not a matched tactical setup.

## a sensor spinout is different

removing the filter tail prevents additional distortion after a large report; it does not recover the sensor's missing or incorrect direction. abrupt reports can also be real flicks. this version does not guess which motion to discard, clamp your turn speed, inject input or alter the signed kernel binary.

keep dpi and game sensitivity fixed. compare in a repeatable practice scene with the same recipe, then use `8 > 20 bypass all` for a driver-effect comparison. bypass is read back from the selected device; enable again through the same menu. if the same spinout happens with effects bypassed, changing the acceleration curve has not removed the underlying fault. note whether it coincides with lifting the mouse or crossing a particular patch of the pad. inspect the sensor opening and surface, charge the mouse, and compare receiver placement as separate checks, not simultaneous tweaks. [trust's model support](https://support.trust.com/en/support/solutions/articles/9000240009-gxt-929-helox-ultra-lightweight-wireless-gaming-mouse-25307).

`3 test hz / gaps` records delivered report timing. a gap may be a pause in movement or another delivery interruption; it does not prove wireless interference or identify a sensor fault.

## network versus local input

riot describes local prediction and separate server corrections: network loss and burst latency can cause visible jumps, and latency affects encounters with other players. a 50–60 ms ping alone does not identify the cause of a mouse-direction jump or disable this driver's curve. compare the same movement in training, inspect frame-time and network graphs, and keep those observations separate from driver readback. [riot's netcode explanation](https://www.riotgames.com/en/news/peeking-valorants-netcode).
