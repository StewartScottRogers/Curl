---
id: BL-2962
title: Fail with exit 3 when an IDN host name cannot be converted so upstream cases 1034 and 1035 pass
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlAuthority.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlParser.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrl.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlTests.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlHostTests.cs, Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1675Functions.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2962 — Fail with exit 3 when an IDN host name cannot be converted so upstream cases 1034 and 1035 pass

## Goal

A URL whose host is not ASCII and cannot be converted to an IDNA A-label - invalid UTF-8 (`invalid-utf8-\xe2\x90.local`, test1034) or a label too long once converted (test1035) - makes Curl exit 3 (`CURLE_URL_MALFORMAT`) before connecting, as curl 8.22.0 does in a UTF-8 locale, so upstream cases 1034 and 1035 pass.

## Context

Found by BL-2861 (2026-10-21), which reports `codeset-utf8` to the harness so these cases run. Both are measured on Windows and exit 7 (Curl connects to the proxy at `%NOLISTENPORT`) where 3 is expected. `CurlUrlHost.ToPunycode` keeps a name `IdnMapping` refuses "as decoded", citing curl 8.21.0 accepting `a%80b` and `a%FFb` (upstream test 763 area); measure real curl (`Record-CurlExchange.ps1`) for both forms - a percent-encoded byte and a raw byte in the URL, and a too-long label - before changing it, and keep whatever case that comment pins passing. Record the decision in an ADR if the two forms differ. Cases at tag `curl-8_22_0`: https://github.com/curl/curl/tree/curl-8_22_0/tests/data

## Acceptance criteria

- [x] A focused unit test in `Curl.Protocol.Abstractions.UnitTests` fails without the fix: a host `IdnMapping` cannot convert is refused as curl refuses it.
- [x] Upstream cases 1034 and 1035 pass and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; every case already listed still passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Protocol.Abstractions.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Measured the Windows reference curl 8.22.0: the two upstream hosts exit 3; `a%80b` exits 7 because that build reads host bytes as Windows-1252. Decided (ADR-0734) to follow curl in a UTF-8 locale: a transfer refuses any name `IdnMapping` cannot convert, the URL API keeps it, so lib1560, unit1675 and the `ManagedCurlUrlHandle` tests keep passing.
- Touches widened to `CurlUrlAuthority.cs`, `CurlUrlParser.cs`, `CurlUrl.cs`, `CurlUrlHostTests.cs` and the unit1675 port's `Unit1675Functions.cs` to thread the refuse/keep switch; no task in Doing names them.
- Measure-CodeQuality.ps1 for Curl.Protocol.Abstractions.UnitLibrary and Curl.Conformance.UnitProgram.UnitLibrary: 0 failing members.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 4c09db1c 583918e6 39956c59.
- 2026-10-21: Doing -> Done. IDN hosts that cannot be converted fail with exit 3; upstream 1034 and 1035 on the ratchet
