# IC-7760 Link — User guide (English)

This guide explains how to use **IC-7760 Link**, the CI-V remote control and waterfall for the ICOM IC-7760. French version: [AIDE.md](AIDE.md).
For a quick overview see the [README](README.md); for the first test on a real radio see [dist/AIDE-POUR-L-AMI.txt](dist/AIDE-POUR-L-AMI.txt) (French).

> The user interface of the program is in French. The names in **bold** below are the labels you see on screen, with their meaning.

## 1. Install and start

1. Download `IC7760Link-X.Y.zip` from the [Releases](https://github.com/F5pbgLudovic/IC7760Link/releases/latest) page.
2. **Extract** the zip to a folder of your choice (Desktop, USB stick…). Do not use Program Files and do not run anything from inside the zip.
3. Run `IC7760Link.exe`. No installation. Settings are stored in a `data` sub-folder created next to the exe.

Windows may show a SmartScreen warning on first launch (unsigned executable): "More info" then "Run anyway".

## 2. Connect

### Try without a radio
In the list at the top right choose **Poste virtuel (simulateur IC-7760)** (virtual radio) and click **CONNECTER** (connect). A simulated IC-7760 answers like the real one (two receivers, live spectrum with SSB, CW, FT8, RTTY, broadcast and QRM).

### With the radio
1. Switch the IC-7760 on and plug the USB cable (Icom USB driver installed).
2. In the radio (SET > Connectors > CI-V): speed **115200** or "Auto", "CI-V USB Echo Back" = OFF.
3. In the software: pick the radio's COM port (Device Manager, "Ports" section), speed **115200**, CI-V address **94** (or **B2**, depending on the radio setting), then **CONNECTER**.
4. Close other programs using the same port (HRD, WSJT-X, Icom software…).

The software **never** drives DTR/RTS: no accidental transmission through the serial line.

> **CI-V address**: if the radio answers with an address other than the one set, the software adopts the radio's address automatically. If nothing connects, see "Troubleshooting".

## 3. The screen

| Area | Content |
|---|---|
| Top left | **MAIN** and **SUB** frequency displays. Click a digit to step it, double-click to type a frequency. |
| Top centre | Two **meters** (S, Po, SWR, ALC, COMP, Vd, Id), as bars or as needles. |
| Top right | Link (COM port, speed, CI-V address, CONNECT), UTC clock, TX (PTT), buttons ?, ⇅ (export/import) and ⚙ (Options). |
| Button rows | Modes (LSB, USB, CW, RTTY, AM, FM, PSK…), filters FIL1-3, DATA1-3, M=S / M↔S, Dualwatch, Split, RIT/XIT, Tuner, step. Then the **scope** bar. |
| Centre | **Spectrum** and **waterfall** of MAIN, SUB or both. |
| Bottom | Console: bands and memories, receiver (AF, RF, SQL, NR, NB, notch, PRE, ATT, AGC…), filter (width, PBT, shape), transmit (power, mic, compressor, drive, monitor, VOX, BK-IN), CW and voice memories. |

The window resizes freely **keeping its proportions**: everything stays visible and there are never scroll bars. In full screen, black bands fill the aspect-ratio difference.

## 4. Tuning the radio

- **Click** on the waterfall or spectrum: tunes the frequency (Shift = 10 Hz steps). Dragging works too.
- **Mouse wheel**: frequency by step (set with "Pas" = step; Shift = 1 kHz).
- **Ctrl + wheel**: zoom (scope width).
- **Click on a frequency label** above a signal: tunes to that signal.
- **Arrow keys**, clickable digits, direct entry (double-click on the display).
- **Esc**: stops a CW transmission in progress.

## 5. The scope (spectrum and waterfall)

### Scope bar buttons
- **SCOPE MAIN / SCOPE SUB / DUAL ↕ / DUAL ↔**: which receiver is shown and how (one, or two scopes stacked or side by side).
- **CENTRE / FIXE / SCROLL-C / SCROLL-F**: the radio's scope modes.
  - **CENTRE**: the frequency stays in the centre, the waterfall scrolls when you change frequency. The cursor stays strictly in the middle.
  - **FIXE**: the frequency window is fixed (the "Fixed Edges" set in the radio), the cursor moves.
  - **SCROLL-C / SCROLL-F**: the radio's scrolling modes.
- **− / ±2.5 … ±500 / +**: scope width (half-width in kHz). "+" zooms in, "−" widens.
- **RÉF − / RÉF +**: scope reference level. **BAL. RAPIDE/MOYEN/LENT**: the radio's sweep speed (fast/medium/slow). **HOLD**: freezes the display.
- **Palette** ("Cascade" list): IC-7760, Thermal, Ocean, Viridis, Amber, Matrix, Rainbow, Grey, **Personnalisée** (custom).
- **AUTO**: automatic level (floor and ceiling follow the noise). The two small knobs set the bottom and top when AUTO is off.

### Waterfall settings
- **VITESSE** (speed, x0.1 to x5): left click = faster, right click = slower; at the end of the range it wraps around.
- **MOY** (averaging, 1 to 20): right click = increase, left click = decrease.
- **PICS** (peaks): left click = toggles peak hold (maxima line) and the frequencies shown on peaks. Right click = hold time: normal, or 1 to 10 s extra (3 s by default).
- **PLAN**: band plan (CW / digital / phone / beacons) under the spectrum.
- **EFFACER**: clears the waterfall history.
- **Right-click menu** on the scope: auto level, **smooth scrolling**, peak hold, band plan, signal frequencies, spectrum fill, averaging, save an image…

### Reading the screen
- **UTC time** (HH:mm:ss) on the left side of the waterfall: one mark every 4 text lines, scrolling with the image.
- **Frequency scale**: ticks at 1/10 of the main interval, discreet grid in the waterfall.
- The red (MAIN) or blue (SUB) **cursor**, the filter **passband**, the RIT cursor and the split transmit frequency are displayed.
- When you change frequency or width, the waterfall history is **re-aligned** (not erased).

## 6. Meters

- Bars: S (MAIN and SUB), Po, SWR, ALC, COMP, Vd, Id.
- **Right click** on the meter area: switches between **bars** and the two **needle meters** styled like the IC-7760 screen.
- On needle meters: the needle shows **S** on receive and the chosen measurement on transmit; **left click** changes the measurement (MET: Po, SWR, ALC, COMP, Vd, Id). In transmit the power needle is averaged, with a red tick for the peak. Mode, filter and BW are shown.

## 6 bis. Tools (⚑ button), TONE button, SWR alert

The **⚑** button (left of ⚙) opens the **Tools** window (4 tabs); the **TONE** button sits next to PTT.

- **Mic / TONE**: choose the PC microphone and the radio's **USB sound card**; the "PC microphone to radio" box streams the microphone to the radio continuously (nothing is played back on the PC). The **TONE** button sends a **1500 Hz** tone to that card for 5 to 60 s (adjustable) with PTT over CI-V or the radio's **VOX**, to tune a coupler or a load; a second click stops it. Default level 15 %. *Set the radio (USB modulation source in data mode) and test on a dummy load.* (The **TUNE** button in the key bar still starts the IC-7760's antenna tuner.)
- **SWR alert**: while transmitting, if the SWR exceeds the threshold (2.0 by default) for the delay (300 ms), **continuous beeps** sound and a red window appears; power can **drop** to a % of the set power (0 % by default = minimum). The alert ends by itself when the SWR stays low (or transmission stops); buttons "Stop the alert" and "Stop and restore power". Tests: beeps, alert, and a **reduced-power TONE test** (original power restored afterwards).
- **Calibration**: the "radio reading (0-255) → watts" and "→ SWR" curves are editable (one `reading = value` line per point, interpolated); "Add the current reading" captures the reading during transmission; the meter scales follow. Defaults: 143 = 100 W, 212 = 200 W; SWR 48 = 1.5, 80 = 2, 120 = 3, 255 = ∞.
- **Update**: "Check now" queries the latest GitHub release; one click replaces only the exe (settings kept, safety copy in `data\sauvegarde-avant-MAJ-…`, old exe kept as `IC7760Link-<version>.exe`) and restarts. "Check at startup" box (off by default). The Mic tab also sets the meters' **peak hold** (900 ms by default).
- Events (TONE, SWR, mic, update) are logged in `data\evenements.log`. All these settings are part of the "application" export.
## 7. Options (⚙ button)

The Options window has a menu on the left:

1. **Waterfall colours** — the 10-colour waterfall gradient (background to strongest signal), "start from an existing palette", and the colours of the MAIN cursor, SUB cursor, spectrum curve, peaks and passband. For each colour: 64-colour grid, R/G/B sliders, hexadecimal code.
2. **Interface colours** — background, panels, buttons, borders, text, dimmed text, accent, cyan, red, green, blue, MAIN and SUB frequency digits, display background, and the **lock colour** (right-click locks). Changes are immediate and saved; "Rétablir" restores the original colours.
3. **Advanced radio settings** — the full CI-V command catalogue (530 entries, including the whole SET menu) with search, read and write. Use with care.
4. **CI-V monitoring and detection** — CI-V frame log, detected CI-V address and automatic radio search (see "Troubleshooting").
5. **Mouse wheel setting** — "Vitesse Potentiomètre": 1 to 10 % of the travel per wheel notch (2 % by default).

## 8. Export / import the configuration (⇅ button)

- **Export**: saves to an `.ini` file four blocks: (1) everything about the application (window, palettes, colours, locks, wheel speed, memories, macros, callsign, COM port…); (2) if the radio is connected, the radio settings shown on screen (frequencies, modes, filters, levels, NR/NB/notch, preamp, attenuator, AGC, power, mic, compressor, VOX, scope); (3) the **IC-7760 internal menu (SET)**: every numeric entry of the menu is read from the radio one by one (a few seconds, "Arrêter" button available, status "Lecture du menu du poste n / N"); (4) the radio's **frequency memories** (channels 1 to 99 and scan edges P1/P2), read exactly as the radio sends them.
- **Import**: after choosing the file, **check boxes** let you restore separately: the application settings (the software restarts), the radio settings shown on screen, the IC-7760 internal menu, the radio's frequency memories (unchecked by default: it **overwrites** the matching radio channels). Radio settings are sent at once if the radio is connected, otherwise on connection (about 6 seconds later). The menu is sent entry by entry, in a few seconds.
- Antenna tuning and transmission are **never** replayed.
- **Not** included: non-numeric menu entries (formats "see p.xx" of the manual: RX HPF/LPF filters, names, texts…). Empty memory channels are not exported (so an empty channel in the file does not clear the radio's channel on import). The memory format has not been checked on a real radio. For a complete radio backup use the radio's own backup function (SD card / USB drive).

## 9. CW, voice memories, bands

- **CW**: speed, pitch, BK delay, free text (Send), four messages F1-F4 (right click to edit, `{C}` = your callsign, typed in the "Votre indicatif" box), Esc = stop.
- **Voice memories T1-T8** and STOP.
- **Bands**: 1.8 to 50 MHz and WWV; each band remembers its last frequency and mode.
- **Memories**: recall, MÉM, ÉCRIRE (write), M→V, EFFACER (clear), VFO, SCAN.

## 10. Locks, wheel speed, power limit

- **Lock (right click)**: a right click on a button or a knob **locks** it: it turns red and no longer reacts (click, drag, wheel) and sends no command. Right click again to unlock. A locked knob also locks its associated buttons (filter width presets, power presets, RIT 0, level AUTO); locking a button does not lock the knob. Locks are saved and restored at start-up. The colour is changed in Options > Interface colours > Verrouillage. (VITESSE, MOY, PICS, F1-F4 and the STOP buttons keep their usual right-click behaviour.)
- **Wheel speed on knobs**: Options (⚙) > **Réglage Molette souris**: 1 to 10 % of the travel per notch (2 % by default). Shift + wheel = finer step.
- **Power limit (P.MAX)**: in the transmit box, the **P.MAX** button asks for a maximum power (1 to 200 W). The POWER knob can no longer exceed it and the higher presets (10 / 50 / 100 / 200 W) are greyed out. If the radio already transmits above it, it is brought back to the limit.

## 11. Troubleshooting

### "Aucune réponse du poste" (no answer from the radio)
1. Check the **COM port** (Device Manager > Ports) and that the radio is on. Close other programs using the port.
2. Open **⚙ > Monitoring et détection CI-V**. The status line shows the CI-V address detected on the link:
   - green: address detected;
   - orange: "the radio answers 94h but the software writes to B2h" → correct the address in the main window;
   - "Aucune réponse" → click **Détecter le poste…**.
3. **Détecter le poste…** (radio disconnected, COM port chosen) tries 7 speeds (115200, 57600, 38400, 19200, 9600, 4800, 230400), reads the radio ID (read-only) and offers to copy the address and speed found.
4. In the radio: check CI-V USB (Unlink if possible), speed, "CI-V USB Echo Back" = OFF.

### The waterfall does not show
Check the CI-V address first (above): a wrong address blocks most commands. If the waterfall stays empty with a correct link, save the monitor log and open an [issue](https://github.com/F5pbgLudovic/IC7760Link/issues).

### Diagnostic for the developer
Without opening the software: `Diagnostic.bat` (or `IC7760Link.exe --diag=COM5,115200,94`) records 12 seconds of CI-V frames (including the raw waterfall frames) in `diagnostic-ci-v.txt`, to attach to an issue.

## 12. Command line (advanced)

| Option | Effect |
|---|---|
| `--virtual` | Starts directly on the virtual radio. |
| `--selftest=report.txt` | Full self-test against the virtual radio; writes the report. |
| `--diag=COM5,115200,94` | Records 12 s of CI-V frames in `diagnostic-ci-v.txt`. |
| `--shot=image.png,ms,actions` | Automatic screenshot (development). |

## 13. Good to know

- The software was developed against a **virtual radio**; first tests on a real IC-7760: connection, waterfall and S-meter confirmed. The rest (writing settings, transmission, menu and memory export/import) remains to be validated radio by radio — **test transmission on a dummy load** at low power.
- The waterfall format (`27 00` frames) is confirmed on a real radio; the filter-width indexing (`1A 03`) is still assumed identical to the IC-7300/7610 family.
- Independent project, **not affiliated with ICOM**. GPL v3 licence.
