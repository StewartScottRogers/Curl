---
id: BL-5413
title: Split Curl.Networking.UnitLibrary/ServerCertificateVerification.cs so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5413 — Split Curl.Networking.UnitLibrary/ServerCertificateVerification.cs so tasks touching it can run in parallel

## Goal

`Curl.Networking.UnitLibrary/ServerCertificateVerification.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 17 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 5 unfinished lane-eligible tasks touching `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs`: BL-5375, BL-5384, BL-5385, BL-5395, BL-5396. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-5375, BL-5384, BL-5385, BL-5395, BL-5396) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Split as a partial class (the primary constructor and shared state stay in ServerCertificateVerification.cs): .ChainErrors.cs (Schannel revocation refusals, tolerated chain errors), .NameChecks.cs (each build's host-name check), .CaCertificates.cs (--cacert/--capath reading, anchor chain rebuild). A partial class has no registry to self-register in. Narrowed touches: BL-5384 and BL-5396 to .CaCertificates.cs, BL-5385 to .ChainErrors.cs, BL-5395 stays on the main file (one task). BL-5375 was already Done. Networking fast tests pass (4218); Measure-CodeQuality: 0 failing members.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: a91861fe 76746532 a0b49a29.
- 2026-11-17: Doing -> Done. Split into partial-class files; waiting tasks' touches narrowed
