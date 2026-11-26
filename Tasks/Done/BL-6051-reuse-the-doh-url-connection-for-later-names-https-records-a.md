---
id: BL-6051
title: Reuse the --doh-url connection for later names, HTTPS records and an HTTP/1.1 second query, as curl does
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-6038]
touches: [Curl.Networking.UnitLibrary/DohDnsResolver.cs, Curl.Networking.UnitLibrary/DohDnsResolver.Exchange.cs, Curl.Networking.UnitLibrary/DohDnsResolver.Http2.cs, Curl.Networking.UnitLibrary/DohDnsResolver.SubTransferTrace.cs, Curl.Networking.UnitLibrary/DohDnsResolver.HttpsRecord.cs, Curl.Networking.UnitTests/DohDnsResolverConnectionReuseTests.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6051 — Reuse the --doh-url connection for later names, HTTPS records and an HTTP/1.1 second query, as curl does

## Goal

Under `--doh-url`, a later lookup and an HTTP/1.1 server's second query go on the DoH connection already open, with the connection numbers and `-v` lines curl prints.

## Context

ADR-1435 (BL-6038) records what curl 8.18.0 on Ubuntu does: a second name in the same run goes on streams 5 and 7 of the first name's HTTP/2 connection, and against an HTTP/1.1 server (nginx) the second query waits and then reuses the first connection (`Reusing existing https: connection with host ...`, both `Connection #1`). `DohDnsResolver` closes each connection once its queries are answered. Measure the HTTPS-record query and the Windows reference before pinning.

## Acceptance criteria

- [x] A unit test in `DohDnsResolverConnectionReuseTests` resolves two names over HTTP/2 and shows the connection count, stream numbers and `-v` lines curl was measured to use.
- [x] A unit test there shows the same for both queries against an HTTP/1.1 DoH server.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.18.0 on Ubuntu (WSL) and on Windows (the WinGet curl.se build, nghttp2) against
  nginx 1.30.8 in Docker; results and decisions are in ADR-1436. The Windows reference behaves as
  Linux does. The HTTPS-record query could not be measured (no curl here lists `HTTPSRR` or `ECH`),
  so it follows the same reuse rules (ADR-1436).
- Choices: one lookup holds a connection at a time; a kept connection that answers nothing is
  replaced without a `-v` line (curl's were not measured); query order stays ADR-0380's.
- Checked `Curl.exe` end to end against the same nginx over three names: HTTP/2 streams 1 to 11 on
  one connection; HTTP/1.1 reuses as measured.
- Touches widened (no task in Doing named them at the claim): `DohResponseReader.cs` (says whether
  the connection stays open), the new `DohConnection.cs` and `DohDnsResolver.ConnectionReuse.cs`,
  the Networking `CLAUDE.md`, the older DoH tests that assumed a connection per query
  (`Cgr286Tests.cs`, the `DohDnsResolver*Tests.cs`, `TcpConnectorShuffledResolverTests.cs`,
  `Fakes/TurnTakingConnector.cs`) and `Curl.Engine.UnitTests/EngineCompositionDohTests.cs`.
- Follow-ups: BL-6052 (dispose the resolver), BL-6053 (`conn_id` numbering under `--doh-url`).
- Checks: `dotnet build` 0 warnings; fast tests pass in all projects; `Measure-CodeQuality.ps1
  -Library Curl.Networking.UnitLibrary` 0 failing members.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 79961f58 94cff58a 3d836d5e.
- 2026-11-26: Doing -> Done. Later names, HTTPS records and an HTTP/1.1 second query reuse the DoH connection (ADR-1436); build clean, fast tests green.
