# Optimizzzer

A Windows 10/11 optimizer built as a single .exe on .NET Framework 4.8 with WPF. It needs no install and no runtime NuGet packages.

## Layout
- `src/Optimizzzer.Core` (netstandard2.0): model, engine, journal/undo, tweak catalog, profiles, reports, CLI parsing. It has no Windows calls; everything goes through the interfaces in `Abstractions/`.
- `src/Optimizzzer.Windows` (net48): real implementations (registry, services, scheduled tasks, WMI, restore point, cleanup, repair, DNS, benchmark).
- `src/Optimizzzer.App` (net48 WPF): UI and CLI entry point.
- `tests/Optimizzzer.Tests` (net8): xUnit tests over Core, using fakes.

## Build
- Core and tests on any OS: `dotnet test tests/Optimizzzer.Tests`
- Everything, including WPF: Windows only (`dotnet build -c Release`), or the GitHub Actions workflow `.github/workflows/build.yml`.

## Rules for tweaks
- Every tweak must be reversible through `Snapshot`/`Revert`, or be marked `Reversible = false` with a warning.
- Every tweak needs ES and EN text, a risk level, a one-line evidence note and an OS range.
- No placebo tweaks (for example `SvcHostSplitThreshold`, `LargeSystemCache`, `NetworkThrottlingIndex` presented as an FPS boost). If unsure, leave it out.
- No em dashes in UI strings (antislop R-02).

## Design
Read `DESIGN.md` before touching UI.

<!-- antislop:start -->
## antislop
For UI, copy, people, mobile layout, or code comments work, read `antislop.md` (core) and then the skill for the task:
- UI / visual: `.claude/skills/antislop-ui/SKILL.md`
- Copy & text: `.claude/skills/antislop-copywriting/SKILL.md`
- People: `.claude/skills/antislop-human/SKILL.md`
- Mobile / responsive: `.claude/skills/antislop-layoutmobile/SKILL.md`
- Code comments: `.claude/skills/antislop-code/SKILL.md`
Before starting, follow the core's "Two Usage Modes" section in strict order: explicit session instruction first, then global preference, then ask. A session instruction always wins. For a resolved mode, say `antislop active: <mode> (session override).` or `antislop active: <mode> (global preference).` once before presenting findings or making edits, using the actual mode and source. Acknowledging the user's request without naming the source does not replace this notice.
Only an explicit choice of antislop during or after selects a session mode. A request to review, audit, or avoid file edits does not select a mode; read the global preference in that case. Another skill's mode does not select antislop's mode.
If the mode is unresolved, ask during/after and end the response; wait for the answer before any UI review, planning, or concept. For read-only tasks, put the active-mode notice only at the start of the final answer, never in progress messages. For editing tasks, announce before the first edit and omit it from the final answer.
To update antislop later: download `antislop.md` again, or run `npx antislop-ai --update` if it was installed as skill folders.
<!-- antislop:end -->
