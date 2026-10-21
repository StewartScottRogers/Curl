---
id: BL-2962
title: Fail with exit 3 when an IDN host name cannot be converted so upstream cases 1034 and 1035 pass
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2962 — Fail with exit 3 when an IDN host name cannot be converted so upstream cases 1034 and 1035 pass

## Goal

A URL whose host is not ASCII and cannot be converted to an IDNA A-label - invalid UTF-8 (`invalid-utf8-\xe2\x90.local`, test1034) or a label too long once converted (test1035) - makes Curl exit 3 (`CURLE_URL_MALFORMAT`) before connecting, as curl 8.22.0 does in a UTF-8 locale, so upstream cases 1034 and 1035 pass.

## Context

Found by BL-2861 (2026-10-21), which reports `codeset-utf8` to the harness so these cases run. Both are measured on Windows and exit 7 (Curl connects to the proxy at `%NOLISTENPORT`) where 3 is expected. `CurlUrlHost.ToPunycode` keeps a name `IdnMapping` refuses "as decoded", citing curl 8.21.0 accepting `a%80b` and `a%FFb` (upstream test 763 area); measure real curl (`Record-CurlExchange.ps1`) for both forms - a percent-encoded byte and a raw byte in the URL, and a too-long label - before changing it, and keep whatever case that comment pins passing. Record the decision in an ADR if the two forms differ. Cases at tag `curl-8_22_0`: https://github.com/curl/curl/tree/curl-8_22_0/tests/data

## Acceptance criteria

- [ ] A focused unit test in `Curl.Protocol.Abstractions.UnitTests` fails without the fix: a host `IdnMapping` cannot convert is refused as curl refuses it.
- [ ] Upstream cases 1034 and 1035 pass and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; every case already listed still passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Protocol.Abstractions.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 4c09db1c 583918e6 39956c59.
