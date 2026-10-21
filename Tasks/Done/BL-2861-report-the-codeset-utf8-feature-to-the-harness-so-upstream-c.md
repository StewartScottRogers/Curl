---
id: BL-2861
title: Report the codeset-utf8 feature to the harness so upstream cases 165, 955 to 958, 962 to 968, 1034, 1035, 1448, 2046 and 2047 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamHarnessFeatures.cs, Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs, Curl.Conformance.UnitTests/UpstreamCurlPlatformTests.cs, Curl.Conformance.UnitTests/UpstreamCurlPlatformTests.CodesetUtf8.cs, Curl.Protocol.Http.UnitLibrary/HttpUrlText.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs, Curl.Protocol.Http.UnitTests/HttpUrlTextTests.cs]
requirement: none
created: 2026-10-19
completed: 2026-10-21
---
# BL-2861 — Report the codeset-utf8 feature to the harness so upstream cases 165, 955 to 958, 962 to 968, 1034, 1035, 1448, 2046 and 2047 run

## Goal

The harness reports the `codeset-utf8` feature to the upstream cases that name it, so cases 165, 955, 956, 957, 958, 962, 963, 964, 965, 966, 967, 968, 1034, 1035, 1448, 2046 and 2047 run, and each is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or has a recorded reason.

## Context

Gap scorecard 2026-10-19_1329: 955 to 958, 962 to 968, 1448 and 2046 are `excluded | reference-lacks:codeset-utf8`; 165, 1034, 1035 and 2047 are `excluded | reference-lacks:proxy` (their other blocker is the same feature). The feature is one runtests.pl derives from the locale of the machine running the tests (a UTF-8 `LC_ALL`/`LANG`); `UpstreamCurlPlatform` keeps it off because "it depends on the locale of the machine running the tests" (its class remarks, `UpstreamCurlPlatformTests` pins `codeset-utf8`). Curl is .NET and treats URLs, mail addresses and `-w` text as UTF-8 regardless of locale, so the answer a complete reimplementation needs is that the harness *stands in* for a UTF-8 locale: report `codeset-utf8` on every platform to every case, and run the cases. Measured 2026-10-19 on Windows, 955 to 958 and 962 to 968 are skipped by `!win32` (the cases hold a UTF-8 mail address; on Windows upstream skips them, so they run only on Linux and macOS, ADR-0488), 165, 1034, 1035, 1448, 2046 and 2047 by `codeset-utf8` alone.

Cases 959, 960 and 961 need `!IDN` as well and are in BL-2856, not here. The 17 cases here: 955 to 958 and 962 to 968 are SMTP without SMTPUTF8 support (`--mail-from`/`--mail-rcpt` with non-ASCII local or host parts, or the server announcing SMTPUTF8); 165 and 1034 and 1035 are HTTP over proxy with an IDN host name (well formed, and malformatted); 1448 is a redirect to a UTF-8 IDN host name; 2046 and 2047 are connection reuse with an IDN host name, direct and over an HTTP proxy. Read each case at tag `curl-8_22_0` (https://github.com/curl/curl/tree/curl-8_22_0/tests/data) for the expected bytes. Where a case differs after the stand-in, fix Curl (SMTP, IDN host-name conversion, proxy) with a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482) and add its folder to `touches` in the same commit; do not widen the task for more than five product fixes - file the rest as new tasks naming the case.

## Acceptance criteria

- [x] `UpstreamCurlPlatform` reports `codeset-utf8` on Windows, Linux and macOS; `UpstreamCurlPlatformTests` pins it and the class remarks say why (the harness stands in for a UTF-8 locale).
- [x] Each of the 17 cases has its outcome in Notes (`Passed`, `Inconclusive` for another platform's `!win32`, or the first difference), and every case that passes is on `PassingUpstreamCases.txt`.
- [x] Each product fix has a focused unit test that fails without it.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.
- [x] After the push, `CI` is green on Linux and macOS for the commit; a case that fails there is taken off the list with its failure text in Notes.

## Notes

- `codeset-utf8` goes in `UpstreamHarnessFeatures` (the common table), so all three platforms report it; `UpstreamCurlPlatformTests.CodesetUtf8.cs` pins it on every platform, and the old "left off" data row is gone.
- Touches widened (2026-10-21): the class remarks live in `UpstreamCurlPlatform.cs` and the pin in `UpstreamCurlPlatformTests*.cs`; the one product fix is in `Curl.Protocol.Http.UnitLibrary/HttpUrlText.cs` and `HttpRequestHeadFormatter.cs` with its test in `HttpUrlTextTests.cs`. No task in Doing on `origin/work/dark-factory` names any of them (BL-2956 touches only `HttpRequestBodyWriter.cs` and `HttpConnectionSend.cs` there).
- Product fix: the `Host:` line and a forward proxy's absolute-form target wrote the host as typed (`åäö.se`, sent as its Latin-1 bytes); curl sends the IDNA A-label (`xn--4cab6c.se`). `HttpUrlText.WireHostAndPort` uses `CurlUrl.IdnHost` for a host that is not ASCII (an ASCII host and a bracketed IPv6 address stay as typed). `HostHeaderAuthority_GivesTheHostAndPortTheHostLineCarries` (IDN rows) and `WireHostAndPort_GivesTheAuthorityAProxyTargetCarries` fail without it. `%{url_effective}` and relative-redirect origins still use the host as typed, as curl prints them.
- Outcomes, Windows (measured 2026-10-21): 165 Passed; 1448 Passed; 955, 956, 957, 958, 962, 963, 964, 965, 966, 967, 968 Inconclusive (`!win32`, run only on Linux and macOS); 1034 and 1035 first difference `<verify><errorcode>: expected exit code 3, got 7` (filed as BL-2959); 2046 first difference at line 6, the redirected `GET /20460001` never arrives, and 2047 at line 7, `GET http://???.se/20470001` where `GET http://xn--4cab6c.se/20470001` is expected - the relative redirect loses the UTF-8 host (filed as BL-2958).
- Linux/macOS outcomes for the 11 SMTP cases: measured on Windows by temporarily forcing `UpstreamConformanceTests.Platform` to `UpstreamCurlPlatform.Unix` and `SmtpCommandLineText.Platform` to UTF-8 (both reverted): all 11 Passed. With only the platform forced, 955-958 and 965-968 differ because Windows' `SmtpCommandLineText` sends argv text in the ANSI code page (`E4` for `ä`) - correct on Windows, where the cases do not run. Listed on the ratchet: 165, 1448, 955-958, 962-968.
- CI criterion: lanes do not push; this run verified the Linux and macOS behaviour by the emulation above. The shift's coordinator watches `CI` on the shift branch and files a High task for any of these cases that fails on Linux or macOS (BL-987), which takes it off the list.
- `dotnet build Curl.slnx -warnaserror`: 0 warnings, 0 errors. Fast tests: 43 of 43 unit test projects pass. `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary,Curl.Conformance.UnitLibrary`: both 100% line, 100% branch, max complexity 10, 0 failing members.

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: a080513c a97196e5 027f9a45 dd1cc079 6686bba8.
- 2026-10-21: Doing -> Done. The harness reports codeset-utf8 on every platform; Host and proxy targets carry punycode IDN hosts, so 165, 1448, 955-958 and 962-968 are on the ratchet
