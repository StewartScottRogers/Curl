---
id: BL-526
title: Answer NTLM challenges for HTTP as the platform's curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-525]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-526 — Answer NTLM challenges for HTTP as the platform's curl does

## Goal

`--ntlm -u user:pass` (and `--anyauth` when NTLM ranks first) sends curl 8.21.0's NTLM Type 1 message, answers the server's Type 2 challenge with a Type 3 message on the same connection, and fails as curl does when the server refuses, using the mechanism BL-525's ADR decides.

## Context

- Prerequisite of audit rows 14, 34 and 39 (see BL-525). ADR-0028 ranks NTLM; `Curl.Authentication.UnitLibrary/RankedHttpAuthenticator.cs` and `HttpAuthSchemeRanking.cs` pick it, and `BasicAndBearerAuthenticator.cs` currently answers nothing for it.
- NTLM authenticates the connection, so the handshake must stay on one connection (`Curl.Protocol.Http.UnitLibrary` keep-alive handling, ADR-0034 for the 401 retry).
- `Record-CurlExchange.ps1` can serve a canned `401` with `WWW-Authenticate: NTLM <type2>` on one connection; the Type 1 bytes curl sends are deterministic, the Type 3 bytes contain a client nonce and time, so pin structure and fields, not whole bytes, where they are random.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (`-HoldOpenMilliseconds` so the second request arrives on the same connection): `--ntlm -u u:p` against a `401 NTLM` then a `401` with a fixed Type 2 then a `200`; request bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Authentication.UnitTests` pin the Type 1 bytes exactly and the Type 3 fields (flags, domain, user, workstation, NTLMv2 response computed from a fixed client nonce and time injected through the seam).
- [ ] A `Curl.Console.UnitTests` test runs the three-leg exchange through the HTTP handler with a fake connector and pins the requests.
- [ ] Platforms where the ADR says NTLM is unavailable give the ADR's stated behaviour, pinned with `OSCondition` tests.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
