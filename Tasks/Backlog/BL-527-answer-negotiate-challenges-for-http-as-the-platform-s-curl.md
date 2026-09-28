---
id: BL-527
title: Answer Negotiate challenges for HTTP as the platform's curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-525]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-527 — Answer Negotiate challenges for HTTP as the platform's curl does

## Goal

`--negotiate -u :` (and `--anyauth` when Negotiate ranks first) answers a `WWW-Authenticate: Negotiate` challenge with a SPNEGO token for `HTTP@<host>`, through the mechanism BL-525's ADR decides, and fails as curl 8.21.0 does when no credentials or no ticket are available.

## Context

- Prerequisite of audit rows 14, 16, 23 and 34 (see BL-525). Ranking: ADR-0028, `Curl.Authentication.UnitLibrary/RankedHttpAuthenticator.cs`.
- Unit tests cannot reach a KDC; the ADR's seam lets tests supply tokens. The only real-curl facts that can be measured without a domain are the failure paths: `--negotiate -u :` against a `401 Negotiate` with no ticket (stderr, exit code, whether a second request is sent).
- `--service-name` and `--delegation` (row 23) are applied later by the service-name task; leave a place for them in the options this task adds.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--negotiate -u : -v` against a `401` with `WWW-Authenticate: Negotiate`, on Windows and on Linux or macOS; request bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Authentication.UnitTests` with a fake token source pin the `Authorization: Negotiate <base64>` header, the service principal name built from the host, and the measured failure behaviour.
- [ ] A `Curl.Console.UnitTests` test runs the exchange through the HTTP handler with fake connector and token source.
- [ ] Each platform's measured failure is pinned in its own `OSCondition` test.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
