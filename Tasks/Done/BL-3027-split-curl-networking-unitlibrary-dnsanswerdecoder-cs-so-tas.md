---
id: BL-3027
title: Split Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-3027 — Split Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs so tasks touching it can run in parallel

## Goal

`Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 2 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 5 unfinished lane-eligible tasks touching `Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs`: BL-2923, BL-2969, BL-3011, BL-3013, BL-3026. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-2923, BL-2969, BL-3011, BL-3013, BL-3026) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

The nested DnsAnswerReader became an internal partial class, one file per unit (DnsAnswerReader.cs, .AnswerRecord, .Address, .CanonicalName, .ServiceRecord, .HttpsRecordData, .Name); code moved verbatim, so no behaviour or complexity change and no full quality run. DnsAnswerDecoder.cs keeps Decode and the class docs (BL-2969 still touches it). Narrowed touches of BL-2923, BL-3011, BL-3013 to the reader files; BL-3026 had none.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 9ea6be46 8fae9362 a0b49a29.
- 2026-10-21: Doing -> Done. DnsAnswerDecoder split into DnsAnswerReader partial files; build and fast tests green
