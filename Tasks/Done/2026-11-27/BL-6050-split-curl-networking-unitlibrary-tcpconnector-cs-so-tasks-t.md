---
id: BL-6050
title: Split Curl.Networking.UnitLibrary/TcpConnector.cs so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitLibrary/TcpConnector.ConnectEntry.cs, Curl.Networking.UnitLibrary/TcpConnector.ConnectTimeout.cs, Curl.Networking.UnitLibrary/TcpConnector.Dialing.cs, Curl.Networking.UnitLibrary/TcpConnector.DirectConnect.cs, Curl.Networking.UnitLibrary/TcpConnector.FilterTrace.cs, Curl.Networking.UnitLibrary/TcpConnector.Http2ProxyTunnel.cs, Curl.Networking.UnitLibrary/TcpConnector.HttpProxyTunnel.cs, Curl.Networking.UnitLibrary/TcpConnector.Multiplexed.cs, Curl.Networking.UnitLibrary/TcpConnector.NameResolution.cs, Curl.Networking.UnitLibrary/TcpConnector.OpenedConnection.cs, Curl.Networking.UnitLibrary/TcpConnector.ProxyAuthorization.cs, Curl.Networking.UnitLibrary/TcpConnector.ProxyChallenge.cs, Curl.Networking.UnitLibrary/TcpConnector.ProxyConnect.cs, Curl.Networking.UnitLibrary/TcpConnector.ProxyDial.cs, Curl.Networking.UnitLibrary/TcpConnector.ProxyTunnelFailure.cs, Curl.Networking.UnitLibrary/TcpConnector.ResolveEntries.cs, Curl.Networking.UnitLibrary/TcpConnector.ResolveFailure.cs, Curl.Networking.UnitLibrary/TcpConnector.SecureOpenedTunnel.cs, Curl.Networking.UnitLibrary/TcpConnector.SocksTunnel.cs, Curl.Networking.UnitLibrary/TcpConnector.TlsHandshake.cs, Curl.Networking.UnitLibrary/TcpConnector.UnixSocket.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6050 — Split Curl.Networking.UnitLibrary/TcpConnector.cs so tasks touching it can run in parallel

## Goal

`Curl.Networking.UnitLibrary/TcpConnector.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 7 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 6 unfinished lane-eligible tasks touching `Curl.Networking.UnitLibrary/TcpConnector.cs`: BL-6019, BL-6024, BL-6028, BL-6033, BL-6039, BL-6049. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Networking.UnitLibrary/TcpConnector.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-6019, BL-6024, BL-6028, BL-6033, BL-6039, BL-6049) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Networking.UnitLibrary/TcpConnector.cs`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Done directly in the session, no subagent stages: a pure move of members with no behaviour change, so there is no new behaviour to plan or test first, and no ADR.
- TcpConnector is one partial class, now one file per unit (`TcpConnector.<Unit>.cs`, 21 new files). Each file registers itself: the SDK compiles every `.cs` file, so a new unit needs no list. `TcpConnector.cs` keeps the primary constructor, the class summary and the configuration fields and properties (374 lines, from 2758).
- Members were moved by a throwaway PowerShell script that checked all 2383 non-blank body lines are in exactly one file afterwards, unchanged. Only `using` directives differ, one set per file.
- AC2: BL-6019, BL-6024, BL-6028, BL-6033, BL-6039 and BL-6049 now name the unit files their change is likely to land in (a lane widens `touches` if it needs another). `hotspots -Min 1` then counts 1 task (this one) for `TcpConnector.cs` and at most 3 for any new file (`ConnectTimeout`: BL-6028, BL-6033, BL-6039).
- Added the new files to `touches` (no task in Doing on origin/work/dark-factory names Curl.Networking). The six task files' front matter is edited because AC2 asks for it.
- Checks: `dotnet build` 0 warnings, 0 errors; fast tests passed in all 115 test assemblies (Curl.Networking.UnitTests 4916 passed, 33 skipped); `dotnet format --verify-no-changes` clean; `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` 0 failing members.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: fa5c285c 741d198e a0b49a29.
- 2026-11-26: Doing -> Done. TcpConnector is one partial-class file per unit, and the six waiting tasks name their own units instead of TcpConnector.cs
