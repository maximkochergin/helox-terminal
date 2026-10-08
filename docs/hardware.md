# hardware findings

helox's input measurements and selected-device aim controls are not restricted to this model. the findings below document the gxt 929 used for initial hardware research; they must not be treated as specifications for other mice. hardware-specific controls require a verified protocol for each supported model.

observed on a connected device on 2026-10-06. machine-specific paths and serial-like identifiers are intentionally omitted.

| property | observed value |
| --- | --- |
| usb vendor | `145f` |
| usb product | `0326` |
| hid product string | `gxt 929 helox` |
| vendor collection | usage page `ffb5`, usage `0001` |
| vendor input report length | 8 bytes |
| vendor output report length | 8 bytes |
| vendor feature report length | 0 bytes |
| mouse collection | usage page `0001`, usage `0002` |
| mouse input report length | 7 bytes |
| mouse output / feature length | 0 / 0 bytes |
| hid device revision | `0200` |
| x/y values | relative, 12 bits, logical range -2047..2047 |
| x/y physical units / range | units 0, physical min/max 0/0 |
| driver-declared button count | 8; advertised value, not physical controls |
| driver-declared sample rate | 0; not reported |

these are windows parsed hid descriptor capabilities. nonzero vendor output length makes a vendor command channel a possibility. it does not establish which commands exist, whether settings are writable, or how reads respond. absence of feature reports does not rule out an output/input protocol.

the physical-scale fields were checked using `hidp_getvaluecaps`. neither x nor y encodes distance units or a physical range. relative counts alone cannot determine counts per inch without a known travel distance or a verified vendor read command.

the official product page's embedded specifications, checked 2026-10-06, list body dimensions 125 x 64 x 38 mm, dpi range 800..4800, four dpi levels, five buttons, a dpi button and 2.4 ghz wireless connectivity. specific intermediate dpi stage values are not listed; the utility does not invent them. `dpi` uses the published body length as a practical travel reference for three approximate passes, rather than requiring ruler marks.

the official trust product page states adjustable sensitivity up to 4800 dpi. the support page lists manuals, datasheets and compliance downloads, with no software download listed when checked. neither page publishes a hardware command protocol. do not substitute windows pointer speed for hardware dpi.

the app opens hid devices with zero desired access to read product strings and parsed descriptors. it does not call `hidd_setfeature`, `hidd_getfeature`, `writefile`, or send vendor reports.

## work required for hardware controls

1. obtain vendor protocol documentation or an actual utility known to support this exact receiver revision.
2. if a utility exists, capture its usb/hid communication while changing one known setting at a time; record before and after values and repeated reads.
3. establish report id, payload, response framing, checksums, supported ranges and persistence behavior. distinguish receiver state from mouse state.
4. implement only understood operations in an isolated device backend. restrict matching to verified devices/revisions and require response/readback checks.
5. verify dpi by independent distance calibration and hz by input measurement; reconnect and repeat to test persistence. keep unsupported settings unavailable.

if no command source exists, further reverse engineering needs hardware/firmware research. blind 8-byte commands are not part of this release.
