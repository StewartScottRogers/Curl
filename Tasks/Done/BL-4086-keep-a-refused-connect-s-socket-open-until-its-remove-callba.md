---
id: BL-4086
title: Keep a refused connect's socket open until its REMOVE callback in the socket-action loop (CGR-034 C_65)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4075]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.Entries.cs, Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitTests/Cgr034RefusedConnectTests.cs, Curl.Engine.UnitLibrary/SocketActionGatedTcpDialer.cs, Curl.Networking.UnitLibrary/ITcpDialer.cs, Curl.Networking.UnitLibrary/TcpDialer.cs, Curl.Networking.UnitTests/TcpDialerSocketCreatedTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4086 — Keep a refused connect's socket open until its REMOVE callback in the socket-action loop (CGR-034 C_65)

## Goal

A transfer to a refused port driven through `curl_multi_socket_action` (CGR-034 probe case `C_65`) gives libcurl's callback sequence `T0 add S1=2 T-1 kick S1=4`: the socket the socket callback named stays a real, pollable socket until its REMOVE callback, and the application's poll on it reports the failed connect.

## Context

Found by BL-4084 (2026-10-29, Windows) rerunning the CGR-034 probe (`.cgr034probe/probe.cs`, source in `Documentation/Reviews/Cgr034ProbeResults.md`) after BL-4075. REF (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`):
`REF C_65: T0 add S1=2 T-1 kick S1=4 | results 7 | real: S1:real | pollErrors>0=False socketEvents>0=True`.
PORT, in 4 of 4 runs:
`PORT C_65: T0 add S1=2 T0 kick S1=4 T-1 | results 7 | real: S1:NOT-A-SOCKET | pollErrors>0=True socketEvents>0=False`.
The managed connect fails and closes its socket before the REMOVE callback and asks for a timer of 0 instead of leaving the socket for the application to poll; libcurl keeps the socket until the action that reads the failure removes it. The earlier BL-3978 run recorded `C_65` as matching, so BL-4075's connect grace in `ManagedMultiTransfer.MovingOn.cs` (`Settle`, `ConnectGraceMilliseconds`) is the first suspect. `C_20`, `C_21`, `C_74`, `C_75` match REF now; keep them matching (`Cgr034KickSettleTests.cs`).

## Acceptance criteria

- [x] A unit test in `Curl.Engine.UnitTests/Cgr034RefusedConnectTests.cs` pins that a refused connect's socket is announced, stays open until its REMOVE callback, and no zero timer is asked for between the announcement and the application's socket action.
- [x] The CGR-034 probe gives `PORT C_65: T0 add S1=2 T-1 kick S1=4 | results 7 | ... real: S1:real` and `C_20`, `C_21`, `C_74`, `C_75` still equal REF.

## Notes

- Cause: the refused loopback connect fails inside BL-4075's 50 ms connect grace, and `TcpDialer` closed the socket in its catch before the socket callback was told of it. Fix (ADR-1002): `ITcpDialer.HoldingFailedSocketsUntil` and `TcpDialer.CloseFailedSocketAsync` await a hook before closing a failed socket; the gated dialer passes `TransferSocketWait.HoldFailedConnectAsync`, released by the application's Out or Error signal, `StopGating` or cancellation.
- Touches widened to `SocketActionGatedTcpDialer.cs` and the three Curl.Networking files: the socket is closed in `TcpDialer`, so the hold must live there. No task in Doing on origin/work/dark-factory named them.
- Probe after the fix (Windows): `PORT C_65: T0 add S1=2 T-1 kick S1=4 | results 7 | real: S1:real`; C_20, C_21, C_74 and C_75 equal the REF rows in Cgr034ProbeResults.md.
- Build clean, fast tests green. Quality run (Engine, Networking): only `EngineComposition.CreateProtocolHandlers` (complexity 12) fails, pre-existing and outside touches, covered by BL-4083.
- Platform check: the tests use stand-in sockets and an unconnected socket's close only; nothing platform-specific.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 37b1443b 3debadd1.
- 2026-10-29: Doing -> Done. A refused connect's socket stays open until its REMOVE callback in the socket-action loop; CGR-034 C_65 matches libcurl
