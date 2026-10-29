---
id: BL-4069
title: Remove the 10 ms RunningTransferCheckInterval timer from ManagedMultiTransfer for CGR-034
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4072, BL-4073, BL-3846, BL-3847, BL-3859, BL-3890, BL-4015, BL-4016, BL-4007, BL-4036, BL-4037, BL-4038]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.Timer.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitTests/ManagedMultiTransferTests.SocketAction.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.Timers.cs, Curl.Engine.UnitTests/Cgr148Tests.cs, Documentation/Decisions]
requirement: none
created: 2026-10-29
completed:
---
# BL-4069 — Remove the 10 ms RunningTransferCheckInterval timer from ManagedMultiTransfer for CGR-034

## Goal

`ManagedMultiTransfer` no longer has the 10 ms `RunningTransferCheckInterval` timer: a running transfer with no socket makes the timer 0, as libcurl's `Curl_expire(data, 0)`.

## Context

Part 2 of 2 of BL-3990 (CGR-034); runs after BL-4068. Remove `RunningTransferCheckInterval`; `CheckIntervalMilliseconds` returns 0 for such a transfer. Update the summaries in `ManagedMultiTransfer.cs` (class summary, field) and `.Timer.cs`, the expectations "timer 10" to "timer 0" and socket actions on `FirstSocket` to `CurlMultiSockets.Timeout` in `SocketAction.cs`, `Timers.cs` line 267 and `Cgr148Tests.cs` line 17, and the Curl.Engine.UnitLibrary CLAUDE.md paragraphs BL-2426 and BL-2427. Record the decision in a new ADR (Decided by Claude under Stewart's delegation).

## Acceptance criteria

- [ ] `RunningTransferCheckInterval` no longer exists in `Curl.Engine.UnitLibrary`.
- [ ] The tests in `ManagedMultiTransferTests.SocketAction.cs`, `.Timers.cs` and `Cgr148Tests.cs` expect the real-socket answers and timer 0, and pass.
- [ ] A new ADR records the decision, marked "Decided by Claude under Stewart's delegation", and the Curl.Engine.UnitLibrary CLAUDE.md no longer describes the synthetic numbering.
- [ ] `Cgr034Tests` still pin `C_20`, `C_74` and `C_75`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3990 (cost cap); BL-3990's Notes hold the full plan and decision.

## Log

- 2026-10-29: Created.
