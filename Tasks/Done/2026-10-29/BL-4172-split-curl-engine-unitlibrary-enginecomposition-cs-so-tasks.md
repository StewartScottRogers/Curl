---
id: BL-4172
title: Split Curl.Engine.UnitLibrary/EngineComposition.cs so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Engine.UnitLibrary/EngineComposition.Extensions.cs, Curl.Engine.UnitLibrary/EngineComposition.ContinueWait.cs, Curl.Engine.UnitLibrary/EngineComposition.PlatformBuilds.cs, Curl.Engine.UnitLibrary/EngineComposition.Ftp.cs, Curl.Engine.UnitLibrary/EngineComposition.ConnectionNumbering.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4172 — Split Curl.Engine.UnitLibrary/EngineComposition.cs so tasks touching it can run in parallel

## Goal

`Curl.Engine.UnitLibrary/EngineComposition.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 6 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 5 unfinished lane-eligible tasks touching `Curl.Engine.UnitLibrary/EngineComposition.cs`: BL-4083, BL-4094, BL-4095, BL-4096, BL-4097. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Engine.UnitLibrary/EngineComposition.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-4083, BL-4094, BL-4095, BL-4096, BL-4097) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Engine.UnitLibrary/EngineComposition.cs`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Split (ADR-1023): `EngineComposition.cs` keeps only `CreateProtocolHandlers`, the handler list (one handler per entry, the registration point). Each other unit moved to its own partial file: `EngineComposition.Extensions.cs` (`ThroughExtension`, `ThroughTransferHooks`, `ExtensionHandlersOver`, the code BL-4094 to BL-4097 changed), `.ContinueWait.cs`, `.PlatformBuilds.cs` (LDAP dialect, SSH algorithms), `.Ftp.cs` (`CreateFtpProtocolHandler`, `FtpDataConnectorOf`) and `.ConnectionNumbering.cs`. Code moved only; no behaviour change. Self-registering handlers (module initializers) were not taken: a static mutable registry is a service locator and each handler needs different services; an options record in place of the parameters would change every caller outside this task's touches. Recorded in ADR-1023.
- `touches` gained the five new partial files, which only this task creates.
- Waiting tasks: BL-4083 and BL-4094 to BL-4097 were all Done (archived, so their front matter is final and was not edited). `hotspots -Min 1` after the split: `Hotspot 3 Curl.Engine.UnitLibrary/EngineComposition.cs: BL-4172, BL-4176, BL-4178` (the last two name the whole project), fewer than 5.
- Verified: `dotnet build` clean; fast tests 53 projects passed, 0 failed (Curl.Engine.UnitTests 2926 passed); `dotnet format --verify-no-changes` on the files clean; `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 522718da 5cd5c6a4 a0b49a29.
- 2026-10-29: Doing -> Done. EngineComposition.cs holds only the handler list; each other unit has its own partial file (ADR-1023)
