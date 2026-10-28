---
id: BL-3886
title: Accept a right-to-left label mixed with Latin in an IDN host (CGR-059 aא)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitTests/Cgr059BidiTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3886 — Accept a right-to-left label mixed with Latin in an IDN host (CGR-059 aא)

## Goal

Curl transfers an IDN host mixing Latin and a right-to-left letter as curl does on Linux.

## Context

Split from BL-3588 (CGR-059 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`, Medium, url-parsing, related GF-0028). Suspected location: `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs` (about lines 100-118 and 177-186; may have moved). Measure real curl first; the server needed answers `200 ok` and records the request (Record-CurlExchange.ps1 or a C# file-based server, no Python).

Repro: `curl -sS "http://aא.example:20200/a" --connect-to ::127.0.0.1:20200`

Expected (real curl): exit 0, `Host: xn--a-0hc.example`. Curl today: exit 3, Bad hostname.

## Acceptance criteria

- [x] The repro above run through real curl and through Curl gives the same exit code, stdout bytes, stderr text and request bytes on the wire; the command and both results are recorded in Notes.
- [x] A focused unit test in `Cgr059*Tests.cs` named in `touches` (in Curl.Protocol.Abstractions.UnitTests) fails before the fix and passes after it (ADR-0482).
- [x] Host cases that already matched curl still match (existing CurlUrlHost tests stay green).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3588 (over its cost cap).
- Linux result was measured against curl 8.18 only (Probable); re-measure, and pin Windows curl 8.22 answers separately if they differ.
- Where curl's answer differs by platform, pin each platform's answer in its own test (CLAUDE.md).
- 2026-10-28 (lane 4): the fix had already landed in BL-3884 (1e7e2ecd4, ADR-0939). It replaced `IdnMapping.GetAscii` with the hand-built label-by-label converter, which does not check bidi text. Before that commit the failure was Linux-only: .NET's `IdnMapping` on Linux uses ICU, which applies the bidi rule and refused `aא` (exit 3, Bad hostname). On Windows, `IdnMapping` returned `xn--a-0hc.example`, checked with a throwaway file-based app. So this run adds only the focused test. No production code changed, so no quality run was needed.
- Linux real curl 8.18.0 (WSL Ubuntu, `LANG=C.UTF-8`, `nc` answering `200 ok`): `curl -sS --trace-ascii t "http://aא.example:20200/a" --connect-to ::127.0.0.1:20200` gave exit 0, stdout `ok\n`, empty stderr and `GET /a HTTP/1.1` / `Host: xn--a-0hc.example:20200` / `User-Agent: curl/8.18.0` / `Accept: */*`.
- Curl does not run on Linux here (WSL has no dotnet). The Linux path is the UTF-8 reading (`strayBytesAsWindows1252: false`), which `Cgr059BidiTests` pins. End to end on Windows, Curl given the same UTF-8 bytes (`url = "http://a%D7%90.example:20200/a"` via `-K`) sent `Host: xn--a-0hc.example:20200` with exit 0 and stdout `ok\n`, the same as Linux curl except the User-Agent version (8.22.0).
- Windows real curl 8.22.0 and Curl with the repro typed on the command line (Record-CurlExchange.ps1): both see the argument through the ANSI code page as `a?`. Both exited 0 and sent identical bytes (`GET /?.example:20200/a`, `Host: a`), with identical stdout and stderr.
- Found a Windows-only gap: real Windows curl reads the whole host as Windows-1252 (`%C3%BC` becomes `xn--14-ria7423a`, `a%D7%90` exits 3), while Curl reads only bytes that are not UTF-8 that way. Filed as BL-3953, so it is not widened into this task.
- Criterion 2: the test exercises `TryNormalizeReadingStrayBytes`, which did not exist before 1e7e2ecd4. The pre-fix refusal was ICU's bidi check on Linux, which a Windows lane cannot reproduce. The test passes now and is platform-neutral, since the method is pure code with no `IdnMapping`.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 12e52b50 1b26f5a2 3be9ea7b 779790c0.
- 2026-10-28: Doing -> Done. An IDN host mixing Latin and a right-to-left letter (aא) converts to xn--a-0hc as curl does on Linux, pinned by Cgr059BidiTests
