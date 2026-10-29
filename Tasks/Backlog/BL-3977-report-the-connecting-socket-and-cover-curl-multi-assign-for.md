---
id: BL-3977
title: Report the connecting socket and cover curl_multi_assign for CGR-034 (C_65, C_21)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890, BL-4006, BL-4007, BL-3990]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitLibrary/SocketActionGatedTcpDialer.cs, Curl.Engine.UnitTests/Cgr034RefusedAssignTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3977 — Report the connecting socket and cover curl_multi_assign for CGR-034 (C_65, C_21)

## Goal

A refused connect and three handles with `curl_multi_assign` give the socket and poll sequences real curl gives (`C_65`, `C_21`).

## Context

Split from BL-3563 (CGR-034). Depends on BL-3976 for the multi wiring. Real curl for `C_65` (refused connect, port 21799 closed): `T0 S=2 T-1 S=4`, result 7; Curl gave `T0 T0 T-1`. BL-3890 makes the connecting socket available from `Curl.Networking`'s `TcpDialer`; report it while the transfer connects. `C_21` runs three easy handles with `curl_multi_assign`: expected sockets are real and assigned data comes back on each socket callback; it has no unit test yet.

## Acceptance criteria

- [ ] A refused-connect transfer reports the connecting socket with `CurlPoll.Out`, then removal, and the timer -1, with result 7 (`T0 S=2 T-1 S=4`), pinned by a test in `Curl.Engine.UnitTests/Cgr034RefusedAssignTests.cs` that fails before the fix.
- [ ] A test with three handles and `curl_multi_assign` pins real sockets and the assigned pointer returned in each socket callback.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3563 (cost cap).

## Log

- 2026-10-28: Created.
