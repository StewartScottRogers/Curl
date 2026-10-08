---
id: BL-1739
title: Write the gap-behaviour analyst that groups failing upstream cases by cause
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1737, BL-1729, BL-1730]
touches: [Gap/Instructions/Behaviour.md, .claude/agents/gap-behaviour.md]
requirement: none
created: 2026-10-08
completed:
---
# BL-1739 — Write the gap-behaviour analyst that groups failing upstream cases by cause

## Goal

`.claude/agents/gap-behaviour.md` (Opus) and `Gap/Instructions/Behaviour.md` exist. The
analyst takes the `behaviour` measurement, which can hold hundreds of failing upstream
cases, and groups the failures by cause. Each group gets a suggestion that names where in
Curl the cause lives, so one filed task can close many cases.

## Context

This is ADR-0433 decision 3. Behaviour is the costly area, so it runs on Opus. The
measurement comes from `ConvertTo-BehaviourMeasurement.ps1` (BL-1729), amended by
`Measure-ReferenceCrossCheck.ps1` (BL-1730). A failed case's `actual` holds the harness's
first difference and its `attributes.keywords` holds upstream's keywords. Follow
`Gap/Instructions/Analyst-Rules.md` and copy the agent shape of
`.claude/agents/gap-options.md` (both BL-1737).

`Behaviour.md`'s method:

1. **Pre-group mechanically.** Normalise each failing case's first difference: strip
   numbers, ports, paths and test numbers to get a signature. Bucket by signature, then by
   keyword. Give the PowerShell one-liner that does this, so the grouping is reproducible
   and costs few tokens.
2. **Find the cause per bucket.** Open two or three of the bucket's cases (`tests/data/test<N>`
   in the release folder the prompt names), read what upstream expects, and find in Curl's
   code what produces the difference. Merge buckets with one cause, and split a bucket with
   two.
3. **One group per cause.** The suggestion names the project and type or file to change,
   and what to change. `touches` names the project folders, usually one
   `Curl.<Area>.UnitLibrary` and its `.UnitTests`. A group of more than 40 cases is split
   by sub-cause where one exists, so a filed task stays sized for one run.
4. **Unmeasured cases are not groups.** Report the per-reason counts in `notes`, together
   with the two or three harness extensions that would measure the most cases (for example
   "an FTP server emulation would measure N cases"). Those extensions are office work,
   filed interactively after BL-1746, not Curl tasks.
5. **`reference-diverges` cases** that are `match` are not gaps. Name their count in `notes`.
6. **Severity**, per ADR-0433 decision 3: `Critical` when the group includes a plain GET,
   POST, `-o`, `-L` or `-u` case whose exit code or output bytes differ; `Medium` for
   message-text-only differences; `High` otherwise.
7. **An example report block** that parses with `ConvertFrom-Json` and satisfies
   `Gap-Format.md`.

The agent uses `model: opus` and `tools: Read, Grep, Glob, Bash`.

## Acceptance criteria

- [ ] `.claude/agents/gap-behaviour.md` exists with `model: opus` and tools limited to `Read, Grep, Glob, Bash`. Its body names `Gap/Instructions/Analyst-Rules.md` and `Gap/Instructions/Behaviour.md`.
- [ ] `Gap/Instructions/Behaviour.md` states steps 1 to 7. The signature one-liner runs under Windows PowerShell 5.1 against BL-1729's fixture measurement (`Gap/Tools/Fixtures/behaviour/`), and its output is shown in the file.
- [ ] The example report block parses with `ConvertFrom-Json` in Windows PowerShell 5.1.
- [ ] No file in this task tells the analyst to read anything under `Audit/`.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
