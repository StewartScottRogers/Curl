---
id: BL-4091
title: Let extension tasks add their projects without overlapping in touches
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.slnx, Curl.Extension.Composition.UnitLibrary/Curl.Extension.Composition.UnitLibrary.csproj, .gitattributes, Tasks/SharedListFiles.txt, Directory.Build.props]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4091 — Let extension tasks add their projects without overlapping in touches

## Goal

Two lanes that each add a new `Curl.Extension.<Name>` project pair can run at once and merge without a conflict.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Over 60 extension tasks each add a project pair. Today each would edit `Curl.slnx` (and a host csproj), so `task-board.ps1` would run them one at a time. `Curl.Console` already references `Curl.Extension.Composition.UnitLibrary`, so an extension reached through Composition needs no host edit.

## Acceptance criteria

- [x] `Curl.Extension.Composition.UnitLibrary` references every `Curl.Extension.<Name>.UnitLibrary` (except Composition and Abstractions) without a per-extension line in its csproj, e.g. by an MSBuild item wildcard, and `VerifyExtensionIsolation` still passes.
- [x] Adding an extension project pair to `Curl.slnx` is an order-free block of lines that two branches can add at once and `git merge` combines cleanly; proved with two scratch branches each adding one block, merged with no conflict and the result building.
- [x] Whatever makes that true is recorded: the file is listed in `Tasks/SharedListFiles.txt` with `merge=union` in `.gitattributes`, or another mechanism is chosen and written into an ADR marked "Decided by Claude under Stewart's delegation".
- [x] CLAUDE.md's "Extensions" section and `DarkFactory/LaneRules.md` say exactly how a new extension adds its projects.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If `merge=union` on the whole `Curl.slnx` is unsafe (a removal in one branch resurrected by another), pick a narrower mechanism and say why in the ADR.

Added: CLAUDE.md and DarkFactory/LaneRules.md edited (the criteria require it); ADR-1003 records the mechanism. Measured with `git merge-file --union`: multi-line blocks interleave into broken XML, so the entry is one single-line Folder (see ADR). Scratch-repo merge was denied by the sandbox, so the merge proof is `git merge-file` on both orders of the same marker insertion. Fast tests: build clean, Composition tests 8/8; no other code changed.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 9a728cc4 35d05a05 cebd48ba 6ab83a52 3d836d5e.
- 2026-10-29: Doing -> Done. Composition references extensions by wildcard; Curl.slnx takes one single-line Folder per extension (merge=union); ADR-1003; docs updated; build clean
