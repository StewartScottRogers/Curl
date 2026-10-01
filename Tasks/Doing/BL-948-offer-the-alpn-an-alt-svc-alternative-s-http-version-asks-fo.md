---
id: BL-948
title: Offer the ALPN an Alt-Svc alternative's HTTP version asks for
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-733]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-948 — Offer the ALPN an Alt-Svc alternative's HTTP version asks for

## Goal

A TLS connect to an Alt-Svc alternative offers exactly the ALPN that alternative's HTTP version asks for (`h2` alone for an `h2` alternative, `http/1.1` alone for an `h1` one), and a same-destination entry makes its version the preferred first attempt, as curl 8.21.0 does.

## Context

- Measured (curl.se 8.18.0 build with ngtcp2, Windows, 2026-09-29; see BL-733's Notes and ADR-0226):
  - With the entry `h1 127.0.0.1 18736 h2 127.0.0.1 18735`, curl prints `* ALPN: curl offers h2` (h2 alone) on the connect to the alternative.
  - With an entry switching to `h1`, curl offers `http/1.1` alone.
  - With an entry naming the origin itself and `h2`/`h1`, curl prefers that version (`neg->preferred`, `lib/cf-https-connect.c` `cf_hc_get_pref_alpn` in 8.21.0); e.g. under `--http3` an `h2` same-destination entry tries TCP h2 before h3.
- Today `Curl.Networking.UnitLibrary/TcpConnector.cs` offers one ALPN list per option group, chosen in `Curl.Console/HttpVersionMapping.cs` (`HttpOverTlsApplicationProtocolsOf`), so on Windows with no version option an `h2` alternative is offered `http/1.1` only and the transfer speaks HTTP/1.1.
- BL-733 set `HttpRequestOptions.AltSvcRoute` and `HttpRequestOptions.Version` from the entry (`Curl.Console/AltSvcTransferCache.cs`); the route reaches the connector on `ConnectTarget` (`Curl.Protocol.Abstractions.UnitLibrary/ConnectTarget.cs`, `AltSvcRoute.cs`; ADR-0208).
- Shape of the fix: a per-connect ALPN list on `ConnectTarget` (or an equivalent seam) that `TcpConnector` honours in place of its option-group list when set, filled by `HttpProtocolHandler` / the Console from the alternative's ALPN; and the same-destination preference applied to the order of attempts.

## Acceptance criteria

- [ ] A `Curl.Networking.UnitTests` test pins that `TcpConnector` offers exactly the per-connect ALPN list when one is set on the target, and its option-group list when none is.
- [ ] Tests pin `h2` alone offered to an `h2` alternative (including on Windows with no version option, where the transfer then speaks HTTP/2) and `http/1.1` alone to an alternative that switches to `h1`, with the verbose line `* ALPN: curl offers h2` / `* ALPN: curl offers http/1.1`.
- [ ] A test pins that under `--http3` an `h2` entry naming the origin itself makes TCP h2 the first attempt, before h3.
- [ ] ADR-0226's section on ALPN (in `Documentation/Planning/Decisions/`) is amended to state the per-connect ALPN and the same-destination preference as built.
- [ ] `dotnet build <project> -warnaserror` is clean for every project changed, and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100% branch coverage for each library changed, with no method over complexity 10 or CRAP 30.

## Notes

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
