---
id: BL-3885
title: Reject a leading-hyphen IDN label and accept empty-label IDN hosts (CGR-059 -ü and ü..)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitTests/Cgr059LabelRulesTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3885 — Reject a leading-hyphen IDN label and accept empty-label IDN hosts (CGR-059 -ü and ü..)

## Goal

Curl handles hyphen and empty labels in IDN hosts as curl does on Linux (libidn2).

## Context

Split from BL-3588 (CGR-059 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`, Medium, url-parsing, related GF-0028). Suspected location: `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs` (about lines 100-118 and 177-186; may have moved). Measure real curl first; the server needed answers `200 ok` and records the request (Record-CurlExchange.ps1 or a C# file-based server, no Python).

Repro: `curl -sS "http://-ü.example:20200/a" --connect-to ::127.0.0.1:20200, and the same with host ü..example`

Expected (real curl): host -ü.example: exit 3, `URL using bad/illegal format or missing URL`; host ü..example: exit 0, `Host: xn--tda..example`. Curl today: -ü gives exit 0 with `Host: xn----eha.example`; ü.. gives exit 3 Bad hostname.

## Acceptance criteria

- [ ] The repro above run through real curl and through Curl gives the same exit code, stdout bytes, stderr text and request bytes on the wire; the command and both results are recorded in Notes.
- [ ] A focused unit test in `Cgr059*Tests.cs` named in `touches` (in Curl.Protocol.Abstractions.UnitTests) fails before the fix and passes after it (ADR-0482).
- [ ] Host cases that already matched curl still match (existing CurlUrlHost tests stay green).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3588 (over its cost cap).
- Linux results were measured against curl 8.18 only (Probable); re-measure, and pin Windows curl 8.22 answers separately if they differ.
- Where curl's answer differs by platform, pin each platform's answer in its own test (CLAUDE.md).

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 12e52b50 1b26f5a2 3be9ea7b 779790c0.
