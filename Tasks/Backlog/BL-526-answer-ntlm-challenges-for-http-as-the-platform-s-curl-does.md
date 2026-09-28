---
id: BL-526
title: Answer NTLM challenges for HTTP as the platform's curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-525, BL-684]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-526 — Answer NTLM challenges for HTTP as the platform's curl does

## Goal

`--ntlm -u user:pass` (and `--anyauth` when NTLM ranks first) sends curl 8.21.0's NTLM Type 1 message, answers the server's Type 2 challenge with a Type 3 message on the same connection, and fails as curl does when the server refuses, using the mechanism BL-525's ADR decides, on every platform.

## Context

- Prerequisite of audit rows 14, 34 and 39 (see BL-525). ADR-0028 ranks NTLM; `Curl.Authentication.UnitLibrary/RankedHttpAuthenticator.cs` and `HttpAuthSchemeRanking.cs` pick it, and `BasicAndBearerAuthenticator.cs` currently answers nothing for it.
- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): NTLM works on every platform, hand-built where the BCL lacks it. `Curl.Authentication.UnitLibrary` references `Curl.Ntlm.UnitLibrary` (BL-682 to BL-684) for the messages and responses; add the reference here.
- NTLM authenticates the connection, so the handshake must stay on one connection (`Curl.Protocol.Http.UnitLibrary` keep-alive handling, ADR-0034 for the 401 retry).
- `Record-CurlExchange.ps1` can serve a canned `401` with `WWW-Authenticate: NTLM <type2>` on one connection; the Type 1 bytes curl sends are deterministic, the Type 3 bytes contain a client nonce and time, so pin structure and fields, not whole bytes, where they are random.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (`-HoldOpenMilliseconds` so the second request arrives on the same connection): `--ntlm -u u:p` against a `401 NTLM` then a `401` with a fixed Type 2 then a `200`; request bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Authentication.UnitTests` pin the Type 1 bytes exactly and the Type 3 fields (flags, domain, user, workstation, NTLMv2 response computed from a fixed client nonce and time injected through the seam).
- [ ] A `Curl.Console.UnitTests` test runs the three-leg exchange through the HTTP handler with a fake connector and pins the requests.
- [ ] NTLM works on Windows, Linux and macOS through the route BL-525's ADR gives each platform (the hand-built `Curl.Ntlm.UnitLibrary`, BL-683 and BL-684, wherever the ADR uses it); no platform refuses it, and where platforms' measured bytes or messages differ each is pinned in its own `OSCondition` test.
- [ ] `curl -V` lists `NTLM` among the features on every platform (ADR-0021), with a test.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
