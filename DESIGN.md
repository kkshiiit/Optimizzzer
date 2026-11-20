# DESIGN.md: Optimizzzer

The owner's brief, transcribed: accessible, minimalist, smooth and visually fast, with icons that clearly show what each thing is. It runs on Windows 10 and 11 and installs nothing.

Dial: ENERGY 1 / RHYTHM 2 / MOTION 2 (MOTION 1 and no shadows in lite mode)

Design Read: a Windows desktop utility for people tuning their own PC (from beginners to power users), in a calm, native, information-first style with spatial layering (v1.1, owner request: "Spatial and minimalism combined").

## Decisions (one line each, R-31)

| Area | Decision | Reason |
|---|---|---|
| Typeface | Segoe UI Variable Text (Win11), falling back to Segoe UI (Win10) | It is the system face: it reads well at small sizes, ships with Windows, and makes the app feel like part of the OS. |
| Type scale | 12 / 14 / 16 / 20 / 28, weights 400 and 600 only | There are few steps so hierarchy comes from size and weight, not from decoration. |
| Icons | Segoe Fluent Icons with Segoe MDL2 Assets as the fallback; glyphs only | The font is built into Windows, renders crisply at any DPI, and each glyph maps to the concept it labels. |
| Palette | Neutrals plus one accent (the user's Windows accent color, adjusted at runtime until it passes AA) | Using the system accent makes the app feel native and avoids inventing a brand that does not exist. |
| Status colors | Amber = Moderate, red = Advanced, green = Applied. Always paired with an icon and a word. | Risk is the most important signal in an optimizer, and it is never shown by color alone. |
| Theme | Dark by default (owner's choice), with Light and System (follows Windows) one click away in Settings; both themes are fully styled | The owner wants a dark tool out of the box; neither mode is a lesser one. |
| Layout | Left navigation rail (Wizard, then Tools), content on the right, one primary action per screen at the bottom right | One focal point per screen; the same place for "next" every time builds muscle memory. |
| Layout responsive | Below 1100 px wide (or when the user collapses it) the menu shows icons only with tooltips and pages use 20 px margins; the window starts sized to the screen; sheets and dialogs keep header and buttons fixed while the middle scrolls | It must work on 1366x768 laptops and small screens without hiding the title bar or the main button. |
| Lists | Flat rows with dividers, not cards | Tweaks are a long, scannable list, and rows show more of them at once while scrolling faster. |
| Radius | 4 px for controls, 8 px for panels | This is the Windows 11 convention: varied, not pill-shaped. |
| Layers (Spatial) | Window background a step darker than the content surface; content floats on a 12 px card with one soft shadow; dialogs, sheets and toasts sit one level higher | Depth tells the eye where the content and the current task are, without decoration. |
| Shadow / glass / glow | One soft shadow per floating surface (content, dialog, toast), never per list row; no glass, no glow | Shadow marks elevation (R-12) and stays cheap: at most three on screen. |
| Motion | 160 ms fade plus an 8 px rise on page and overlay entry, 120 ms card lift on hover, one scale-in check at the end of an apply, smooth progress bars; no loops | Motion confirms where things come from and that an action finished; it never delays input. |
| Lite mode | Automatic when WPF has no hardware rendering, RAM is under 4 GB, it is a remote session, or Windows animations are off; can be forced on or off in Settings. It turns off every shadow and animation | The app must open and stay smooth on the slowest PC that runs Windows 10. |
| Accent presets | Windows accent (default), Blue, Green, Violet, Orange; each adjusted at runtime to pass AA (checked by the self-test in both themes) | People can personalise without breaking contrast. |
| Focus | A 2 px focus ring in the accent color on every control | Keyboard users must always see where they are (R-32). |
| Hit targets | Rows and buttons at least 40 px tall, with icon buttons 40x40 inside a 44 px row | These sizes are comfortable for mouse, touch and pen. |
| Identity motif | The risk marker: an icon plus a word at the start of every tweak row, repeated in the review screen and the report | It is the one element that makes this app recognisable, and it carries the main idea: know what you are changing. |
| Logo | None. The product name is set as text. | No logo was supplied (R-23). |

## Tokens

The text pairs below were checked with `.claude/skills/antislop-human/contrast-check.py`.

| Token | Light | Dark | Use |
|---|---|---|---|
| Bg | #EDEEF1 | #161618 | Window and navigation (the lowest layer) |
| Surface | #FFFFFF | #232327 | Floating content panel |
| SurfaceRaised | #FFFFFF | #2B2B30 | Dialogs, sheets, toasts (the highest layer) |
| SurfaceAlt | #F0F1F3 | #2E2E33 | Hover, cards, chips |
| Divider | #E2E3E7 | #38383E | Decorative lines only |
| ControlBorder | #80808A (3.4:1+) | #7C7C86 (3.8:1+) | Checkbox and input outlines (WCAG 1.4.11) |
| Text | #1B1B1F (14.8:1) | #F2F2F4 (16:1) | Body |
| TextMuted | #5A5A63 (5.9:1 or more) | #A8A8B0 (5.7:1 or more) | Secondary text |
| AccentFallback | #0063B1 (white text 6.1:1) | #4CC2FF (black text 10.5:1) | Used when the system accent cannot reach AA |
| Moderate | #8A5300 (5.5:1 or more) | #F2B24B (8:1) | Moderate risk |
| Advanced | #B3261E (5.6:1 or more) | #FF8A80 (6.6:1) | Advanced risk, errors |
| Success | #1E7B34 (5.3:1) | #6CCB7F (7.5:1) | Applied state |

## Copy

- Plain words, second person, in Spanish and English.
- No em dashes in UI text.
- No buzzwords.
- Buttons say exactly what happens: "Create restore point and apply 12 changes", not "Optimize now".
- Numbers are only shown when they were measured on this PC.
