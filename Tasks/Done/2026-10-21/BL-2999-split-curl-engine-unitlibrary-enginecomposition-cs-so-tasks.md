---
id: BL-2999
title: Split Curl.Engine.UnitLibrary/EngineComposition.cs so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EngineComposition.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2999 — Split Curl.Engine.UnitLibrary/EngineComposition.cs so tasks touching it can run in parallel

## Goal

`Curl.Engine.UnitLibrary/EngineComposition.cs` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 9 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 5 unfinished lane-eligible tasks touching `Curl.Engine.UnitLibrary/EngineComposition.cs`: BL-2834, BL-2979, BL-2995, BL-2996, BL-2998. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Curl.Engine.UnitLibrary/EngineComposition.cs` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-2834, BL-2979, BL-2995, BL-2996, BL-2998) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Curl.Engine.UnitLibrary/EngineComposition.cs`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- `EngineComposition` is a static class of factories, not a registration table, so "registers itself" here means a `partial` class: each area is its own file and the compiler joins them. Split by area, every member moved unchanged: `EngineComposition.cs` (class summary, `CreateProtocolHandlers` and the protocol handler helpers), `.Authentication.cs` (HTTP and SASL authenticators, security-context factories, KDC timeouts), `.Dns.cs` (DNS and DoH resolvers, `DohUrlOf`, `CreateDohConnector`, `DialingResolverOf`, `CURL_DNS_SERVER`), `.Traces.cs` (the `Traces*` predicates), `.Transports.cs` (`CreateTransports`, `CreateTlsProvider`, dialers, socket options, the datagram QUIC dialer), `.Connectors.cs` (pooling, TCP and UDP connectors and their option readers), `.Proxy.cs` (pre-proxy, HAProxy, CONNECT tunnel options) and `.DebugBuild.cs` (the `CURL_ALTSVC_HTTP`, `CURL_HSTS_HTTP` and `CURL_TIME` readers). Unused usings were trimmed per file with `dotnet format --diagnostics IDE0005`.
- Narrowed `touches`: BL-2979 to `.Dns.cs`; BL-2995 and BL-2996 to `.Connectors.cs` (BL-2996 also has a note on reapplying its stashed `EngineComposition.cs` hunks there); BL-2834 keeps `EngineComposition.cs` and adds `.Authentication.cs`, since its fixed nonce has to reach both `CreateHttpAuthenticator`/`CreateSaslAuthenticator` and `CreateProtocolHandlers`. BL-2998's `touches` is empty, so it overlaps every path. `hotspots -Min 1` now counts 3 for `EngineComposition.cs` (BL-2834, BL-2998 and this task), 2 once this task is Done.
- Measured: `dotnet build` clean; `Curl.Engine.UnitTests` fast tests 2207 passed, 5 skipped; `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 522718da b00fc27d a0b49a29.
- 2026-10-21: Doing -> Done. EngineComposition split into partial files by area; waiting tasks' touches narrowed
