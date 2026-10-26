---
id: BL-3884
title: Accept a percent-encoded non-UTF-8 byte in an IDN host (CGR-059 a%FFb)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitTests/Cgr059PercentEncodedHostTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3884 — Accept a percent-encoded non-UTF-8 byte in an IDN host (CGR-059 a%FFb)

## Goal

Curl transfers `http://a%FFb:20200/a` as curl 8.22.0 does on Windows.

## Context

Split from BL-3588 (CGR-059 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`, Medium, url-parsing, related GF-0028). Suspected location: `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs` (about lines 100-118 and 177-186; may have moved). Measure real curl first; the server needed answers `200 ok` and records the request (Record-CurlExchange.ps1 or a C# file-based server, no Python).

Repro: `curl -sS "http://a%FFb:20200/a" --connect-to ::127.0.0.1:20200`

Expected (real curl): exit 0, request has `Host: xn--ab-6ka`. Curl today: exit 3, `URL rejected: Bad hostname`.

## Acceptance criteria

- [ ] The repro above run through real curl and through Curl gives the same exit code, stdout bytes, stderr text and request bytes on the wire; the command and both results are recorded in Notes.
- [ ] A focused unit test in `Cgr059*Tests.cs` named in `touches` (in Curl.Protocol.Abstractions.UnitTests) fails before the fix and passes after it (ADR-0482).
- [ ] Host cases that already matched curl still match (existing CurlUrlHost tests stay green).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3588 (over its cost cap).
- Continues the stashed work of BL-3588.
- Where curl's answer differs by platform, pin each platform's answer in its own test (CLAUDE.md).

## Log

- 2026-10-26: Created.
