# hardware findings

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

these are windows parsed hid descriptor capabilities. nonzero vendor output length makes a vendor command channel a possibility. it does not establish which commands exist, whether settings are writable, or how reads respond. absence of feature reports does not rule out an output/input protocol.

the official trust product page states adjustable sensitivity up to 4800 dpi. the support page lists manuals, datasheets and compliance downloads, with no software download listed when checked. neither page publishes a hardware command protocol. do not substitute windows pointer speed for hardware dpi.

the app opens hid devices with zero desired access to read product strings and parsed descriptors. it does not call `hidd_setfeature`, `hidd_getfeature`, `writefile`, or send vendor reports.

## work required for hardware controls

1. obtain vendor protocol documentation or an actual utility known to support this exact receiver revision.
2. if a utility exists, capture its usb/hid communication while changing one known setting at a time; record before and after values and repeated reads.
3. establish report id, payload, response framing, checksums, supported ranges and persistence behavior. distinguish receiver state from mouse state.
4. implement only understood operations in an isolated device backend. restrict matching to verified devices/revisions and require response/readback checks.
5. verify dpi by independent distance calibration and hz by input measurement; reconnect and repeat to test persistence. keep unsupported settings unavailable.

if no command source exists, further reverse engineering needs hardware/firmware research. blind 8-byte commands are not part of this release.
