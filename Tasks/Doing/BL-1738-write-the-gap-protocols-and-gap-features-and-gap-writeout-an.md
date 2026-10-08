---
id: BL-1738
title: Write the gap-protocols and gap-features and gap-writeout and gap-exitcodes analysts
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1737, BL-1724, BL-1725, BL-1726]
touches: [Gap/Instructions/Protocols.md, Gap/Instructions/Features.md, Gap/Instructions/WriteOut.md, Gap/Instructions/ExitCodes.md, .claude/agents/gap-protocols.md, .claude/agents/gap-features.md, .claude/agents/gap-writeout.md, .claude/agents/gap-exitcodes.md]
model: sonnet
requirement: none
created: 2026-10-08
completed:
---
# BL-1738 — Write the gap-protocols and gap-features and gap-writeout and gap-exitcodes analysts

## Goal

The four inventory analysts exist as Haiku agents, each with a method file:
`.claude/agents/gap-protocols.md`, `gap-features.md`, `gap-writeout.md` and
`gap-exitcodes.md`, with `Gap/Instructions/Protocols.md`, `Features.md`, `WriteOut.md`
and `ExitCodes.md`.

## Context

This is ADR-0433 decision 3. These areas' measurements are already itemised and mostly
one-to-one, so Haiku is enough: the analyst groups and writes suggestions, nothing more.
Follow the shape BL-1737 set: `Gap/Instructions/Analyst-Rules.md` binds every analyst, and
`.claude/agents/gap-options.md` is the agent template. The measurements come from
`Measure-VersionGap.ps1` (BL-1724; `protocols` and `features`), `Measure-WriteOutGap.ps1`
(BL-1725) and `Measure-ExitCodeGap.ps1` (BL-1726).

Each method file gives:

- **Grouping.**
  - Protocols: one group per scheme family, so `ftp` and `ftps` go together and the
    suggestion names its `Curl.Protocol.<Name>.UnitLibrary`.
  - Features: one group per feature, except that features which stand for the same
    capability (for example `HTTP2` and `h2c` handling) go together.
  - Write-out: unknown variables go in one group per Curl component that would supply
    them, and value differences one group per variable. Suggestions name
    `Curl.Output.UnitLibrary`, where the `-w` variables are written.
  - Exit codes: missing codes in one group (`Curl.Protocol.Abstractions.UnitLibrary`'s
    `CurlExitCode`), and text differences in one group (`Curl.Console/CurlEasyErrorText.cs`).
- **Severity**, per ADR-0433 decision 3. A missing scheme, feature, variable or code is
  `High`. A differing text or value is `Medium`. A scheme listed by Curl that the
  reference build lacks is `Medium`, with the suggestion to stop listing it (ADR-0021).
- **An example report block** that parses with `ConvertFrom-Json` and satisfies
  `Gap-Format.md`.

Agents use `model: haiku` and `tools: Read, Grep, Glob, Bash`. Each body names
`Analyst-Rules.md` and its own method, and ends with the report block.

## Acceptance criteria

- [ ] The four agent files exist with `model: haiku` and tools limited to `Read, Grep, Glob, Bash`. Each body names `Gap/Instructions/Analyst-Rules.md` and its own method file.
- [ ] The four method files each state their grouping rules, the severity mapping above, and an example report block that parses with `ConvertFrom-Json` in Windows PowerShell 5.1.
- [ ] No file in this task tells an analyst to read anything under `Audit/`, or to put a `Gap/`, `Audit/` or `.claude/` path in `touches`.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
