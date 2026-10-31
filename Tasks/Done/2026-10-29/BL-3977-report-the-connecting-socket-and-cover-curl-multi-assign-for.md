---
id: BL-3977
title: Report the connecting socket and cover curl_multi_assign for CGR-034 (C_65, C_21)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890, BL-4015, BL-4016, BL-4007, BL-4072, BL-4073, BL-4069]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.Perform.cs, Curl.Engine.UnitLibrary/SocketActionGatedTcpDialer.cs, Curl.Engine.UnitTests/Cgr034RefusedAssignTests.cs, Curl.Engine.UnitTests/Cgr034Tests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-29
---
# BL-3977 — Report the connecting socket and cover curl_multi_assign for CGR-034 (C_65, C_21)

## Goal

A refused connect and three handles with `curl_multi_assign` give the socket and poll sequences real curl gives (`C_65`, `C_21`).

## Context

Split from BL-3563 (CGR-034). Depends on BL-3976 for the multi wiring. Real curl for `C_65` (refused connect, port 21799 closed): `T0 S=2 T-1 S=4`, result 7; Curl gave `T0 T0 T-1`. BL-3890 makes the connecting socket available from `Curl.Networking`'s `TcpDialer`; report it while the transfer connects. `C_21` runs three easy handles with `curl_multi_assign`: expected sockets are real and assigned data comes back on each socket callback; it has no unit test yet.

## Acceptance criteria

- [x] A refused-connect transfer reports the connecting socket with `CurlPoll.Out`, then removal, and the timer -1, with result 7 (`T0 S=2 T-1 S=4`), pinned by a test in `Curl.Engine.UnitTests/Cgr034RefusedAssignTests.cs` that fails before the fix.
- [x] A test with three handles and `curl_multi_assign` pins real sockets and the assigned pointer returned in each socket callback.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3563 (cost cap).
- Found on claim: the fix had already landed (aa0f98b44 and BL-3890, BL-4069): `SocketActionGatedTcpDialer` publishes the connecting socket, and both tests (refused connect `T0 S=2 T-1 S=4` with result 7; three transfers with `curl_multi_assign`) were already in `Cgr034Tests.cs` and passing. No production change was needed.
- Moved those two tests into `Cgr034RefusedAssignTests.cs`, the file the criterion names, as a part of the now partial `Cgr034Tests` so they keep sharing its stand-ins (`SocketDialer`, `EngineOver`, `PatientMultiOf`); added `Cgr034Tests.cs` to touches for that (no other task in Doing on origin/work/dark-factory names it).
- Fails before the fix: with the dialer publishing to a no-op (`PublishingSocketsTo(_ => { })`) the refused-connect test fails at `Assert.AreEqual(new CurlSocket(Handle), connecting)`; restored, it passes.
- No quality run: production code is unchanged.

## Log

- 2026-10-28: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: f153c03a 60788fbd 779790c0.
- 2026-10-29: Doing -> Done. The refused-connect (T0 S=2 T-1 S=4, result 7) and three-handle curl_multi_assign socket sequences are pinned in Cgr034RefusedAssignTests.cs
