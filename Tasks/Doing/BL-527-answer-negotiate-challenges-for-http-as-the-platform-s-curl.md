---
id: BL-527
title: Answer Negotiate challenges for HTTP as the platform's curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-525, BL-691, BL-692, BL-684, BL-694]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-527 — Answer Negotiate challenges for HTTP as the platform's curl does

## Goal

`--negotiate -u :` (and `--anyauth` when Negotiate ranks first) answers a `WWW-Authenticate: Negotiate` challenge with a SPNEGO token for `HTTP@<host>`, through the mechanism BL-525's ADR decides, carrying a Kerberos token (or NTLM when SPNEGO falls back to it), and fails as curl 8.21.0 does when no credentials or no ticket are available, on every platform.

## Context

- Prerequisite of audit rows 14, 16, 23 and 34 (see BL-525). Ranking: ADR-0028, `Curl.Authentication.UnitLibrary/RankedHttpAuthenticator.cs`.
- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): Negotiate works on every platform, hand-built where the BCL lacks it. Pieces: SPNEGO (BL-692), the GSS-API Kerberos mechanism (BL-691, `Curl.Kerberos.UnitLibrary`), NTLM (BL-684, `Curl.Ntlm.UnitLibrary`); `Curl.Authentication.UnitLibrary` references both libraries.
- This task also composes the production adapters the Kerberos library needs, in `Curl.Networking.UnitLibrary`: the KDC transport (UDP, then TCP with the length prefix, RFC 4120 section 7.2.1) behind a thin datagram/TCP seam, and the SRV lookup over the hand-built DNS client (BL-694); both wired in `Curl.Console/CurlTransports.cs` or `CurlComposition.cs`.
- Unit tests cannot reach a KDC; the ADR's seam lets tests supply tokens. The only real-curl facts that can be measured without a domain are the failure paths: `--negotiate -u :` against a `401 Negotiate` with no ticket (stderr, exit code, whether a second request is sent).
- `--service-name` and `--delegation` (row 23) are applied later by the service-name task; leave a place for them in the options this task adds.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--negotiate -u : -v` against a `401` with `WWW-Authenticate: Negotiate`, on Windows and on Linux or macOS; request bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Authentication.UnitTests` with a fake token source pin the `Authorization: Negotiate <base64>` header, the service principal name built from the host, and the measured failure behaviour.
- [ ] A `Curl.Console.UnitTests` test runs the exchange through the HTTP handler with fake connector and token source.
- [ ] Each platform's measured failure is pinned in its own `OSCondition` test; a successful Kerberos exchange (fake KDC through the transport seam, fake acceptor) passes on every platform, and no platform refuses Negotiate.
- [ ] `Curl.Networking.UnitTests` cover the KDC transport adapter's UDP-to-TCP switch and framing through its seam.
- [ ] `curl -V` lists `Kerberos`, `SPNEGO` and `GSS-API` among the features on every platform (ADR-0021), with a test.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
