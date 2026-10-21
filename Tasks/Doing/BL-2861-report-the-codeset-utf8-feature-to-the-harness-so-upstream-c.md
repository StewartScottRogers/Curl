---
id: BL-2861
title: Report the codeset-utf8 feature to the harness so upstream cases 165, 955 to 958, 962 to 968, 1034, 1035, 1448, 2046 and 2047 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamHarnessFeatures.cs]
requirement: none
created: 2026-10-19
completed:
---
# BL-2861 — Report the codeset-utf8 feature to the harness so upstream cases 165, 955 to 958, 962 to 968, 1034, 1035, 1448, 2046 and 2047 run

## Goal

The harness reports the `codeset-utf8` feature to the upstream cases that name it, so cases 165, 955, 956, 957, 958, 962, 963, 964, 965, 966, 967, 968, 1034, 1035, 1448, 2046 and 2047 run, and each is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or has a recorded reason.

## Context

Gap scorecard 2026-10-19_1329: 955 to 958, 962 to 968, 1448 and 2046 are `excluded | reference-lacks:codeset-utf8`; 165, 1034, 1035 and 2047 are `excluded | reference-lacks:proxy` (their other blocker is the same feature). The feature is one runtests.pl derives from the locale of the machine running the tests (a UTF-8 `LC_ALL`/`LANG`); `UpstreamCurlPlatform` keeps it off because "it depends on the locale of the machine running the tests" (its class remarks, `UpstreamCurlPlatformTests` pins `codeset-utf8`). Curl is .NET and treats URLs, mail addresses and `-w` text as UTF-8 regardless of locale, so the answer a complete reimplementation needs is that the harness *stands in* for a UTF-8 locale: report `codeset-utf8` on every platform to every case, and run the cases. Measured 2026-10-19 on Windows, 955 to 958 and 962 to 968 are skipped by `!win32` (the cases hold a UTF-8 mail address; on Windows upstream skips them, so they run only on Linux and macOS, ADR-0488), 165, 1034, 1035, 1448, 2046 and 2047 by `codeset-utf8` alone.

Cases 959, 960 and 961 need `!IDN` as well and are in BL-2856, not here. The 17 cases here: 955 to 958 and 962 to 968 are SMTP without SMTPUTF8 support (`--mail-from`/`--mail-rcpt` with non-ASCII local or host parts, or the server announcing SMTPUTF8); 165 and 1034 and 1035 are HTTP over proxy with an IDN host name (well formed, and malformatted); 1448 is a redirect to a UTF-8 IDN host name; 2046 and 2047 are connection reuse with an IDN host name, direct and over an HTTP proxy. Read each case at tag `curl-8_22_0` (https://github.com/curl/curl/tree/curl-8_22_0/tests/data) for the expected bytes. Where a case differs after the stand-in, fix Curl (SMTP, IDN host-name conversion, proxy) with a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482) and add its folder to `touches` in the same commit; do not widen the task for more than five product fixes - file the rest as new tasks naming the case.

## Acceptance criteria

- [ ] `UpstreamCurlPlatform` reports `codeset-utf8` on Windows, Linux and macOS; `UpstreamCurlPlatformTests` pins it and the class remarks say why (the harness stands in for a UTF-8 locale).
- [ ] Each of the 17 cases has its outcome in Notes (`Passed`, `Inconclusive` for another platform's `!win32`, or the first difference), and every case that passes is on `PassingUpstreamCases.txt`.
- [ ] Each product fix has a focused unit test that fails without it.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.
- [ ] After the push, `CI` is green on Linux and macOS for the commit; a case that fails there is taken off the list with its failure text in Notes.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: a080513c a97196e5 027f9a45 dd1cc079 6686bba8.
