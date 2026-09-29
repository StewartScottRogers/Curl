---
id: BL-601
title: Parse --proxy-basic, --proxy-digest, --proxy-ntlm, --proxy-negotiate and --proxy-anyauth
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-601 — Parse --proxy-basic, --proxy-digest, --proxy-ntlm, --proxy-negotiate and --proxy-anyauth

## Goal

The five proxy authentication switches parse into a proxy auth-scheme set on `CommandLineOptions`, combined as curl 8.21.0 combines them, mirroring how `--basic`, `--digest`, `--ntlm`, `--negotiate` and `--anyauth` already set the server scheme set.

## Context

- Conformance audit 2026-09-28, row 14 (Major). Using them is BL-602 to BL-604.
- The server-side switches are rows in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`; ADR-0026 records how they parse. `HttpAuthSchemes` is in Abstractions.

## Acceptance criteria

- [x] Every switch, and combinations (`--proxy-digest --proxy-basic`, `--proxy-anyauth --proxy-basic`), are covered by `Curl.Cli.UnitTests` with the set curl keeps (measure any doubtful combination with `Record-CurlExchange.ps1` as a proxy and record it in Notes).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured curl 8.21.0 (Schannel, Windows) 2026-09-28 with `Record-CurlExchange.ps1` as the proxy, `-s -x http://127.0.0.1:<P> -U u:p <switches> http://example.test/`, Proxy-Authorization of the first request:
  - no switch, `--proxy-basic --no-proxy-basic`, `--proxy-digest --no-proxy-digest`: `Basic dTpw`.
  - `--proxy-digest --proxy-ntlm`: an NTLM type-1 message at once (so NTLM alone, not NTLM|Digest).
  - `--proxy-digest --proxy-basic`, `--proxy-basic --proxy-digest`, `--proxy-ntlm --proxy-negotiate`, `--proxy-anyauth --proxy-basic`, `--proxy-anyauth --no-proxy-anyauth --proxy-digest`: nothing.
  - Against `407` offering only Basic (`-Connections 2`): `--proxy-digest --proxy-basic` and `--proxy-basic --proxy-digest` stop with `407`; `--proxy-anyauth --proxy-basic` answers `Basic dTpw` and gets `200`.
- Conclusion: unlike the server switches, the proxy switches are independent booleans and curl's tool sets CURLOPT_PROXYAUTH to exactly one choice, first match of anyauth (Any), negotiate, ntlm, digest, basic; none gives libcurl's default Basic. Order on the command line does not matter. `--proxy-anyauth` is negatable, as the alias table already recorded.
- Implemented as `CommandLineOptions.ProxyAuthSchemes` (per-group, like `AuthSchemes`), set by five `NegatableFlag` rows. No ADR: the behaviour is measured, not chosen. Using the set is BL-602 to BL-604.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. The five --proxy-* auth switches parse into CommandLineOptions.ProxyAuthSchemes, one scheme picked as curl 8.21.0 picks it
