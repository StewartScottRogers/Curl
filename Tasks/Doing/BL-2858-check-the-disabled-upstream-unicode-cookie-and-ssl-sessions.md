---
id: BL-2858
title: Check the disabled-upstream, Unicode, cookie and ssl-sessions upstream cases 594, 836, 938, 1182, 1211, 1307, 1801, 2043, 1703, 472, 8 and 777: list those that pass, record the rest
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamWindowsFeatures.cs, Curl.Conformance.UnitLibrary/UpstreamUnixFeatures.cs, Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-10-19
completed:
---
# BL-2858 — Check the disabled-upstream, Unicode, cookie and ssl-sessions upstream cases 594, 836, 938, 1182, 1211, 1307, 1801, 2043, 1703, 472, 8 and 777: list those that pass, record the rest

## Goal

Each of upstream cases 594, 836, 938, 1182, 1211, 1307, 1801, 2043, 1703, 472, 8 and 777, which are off the ratchet only because upstream disables them, the reference curl build lacks a feature, or the platform differs, has been run through Curl's own runner on Windows and is either on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or has a recorded reason.

## Context

Gap scorecard 2026-10-19_1329 and a run on Windows on 2026-10-19:

- Disabled upstream (`excluded | disabled-upstream`; `tests/data/DISABLED` of curl 8.22.0 lists 594, 836, 938, 1182, 1211, 1307, 1801 and 2043): 594 `curl did not finish within 20 seconds`; 836 and 938 differ (836: Curl sent `B004 UID FETCH 2 BODY[]` where the case expects `B004 FETCH 2 BODY[]`; 938: the `AUTH PLAIN` initial response is `AHVzZXIub25lAHNlY3JldA==` where the case expects `dXNlci5vbmUAdXNlci5vbmUAc2VjcmV0`); 1182 is skipped for a `mkdir`/`cp` precheck; 1211 differs (`QUIT` extra); 1307 needs `unittest` (DISABLED says "fnmatch differences are too common to make testing them sensible"); 1801 needs `http/2`, which Windows' Schannel build lacks (see `UpstreamCurlPlatform`, BL-2230); 2043 reaches `revoked.badssl.com` on the internet. Upstream disables them, so the default is to keep them off the ratchet; but a case disabled for flakiness upstream may pass reliably here - run each ten times and list it only if all ten pass.
- 1703 (`platform:windows`, features `Unicode,win32`, a UTF-8 file name in a `file://` URL) and 472 (aws-sigv4 with a Unicode query) need the `Unicode` feature, which `UpstreamCurlPlatform` keeps off because it is absent from Curl's `curl -V` (Windows' `curl.exe` is built with `UNICODE`). Decide in the ADR whether Curl reports `Unicode` on Windows as the reference curl does; if it does, run both.
- 8 ("HTTP with cookie parsing from header file", `excluded | reference-lacks:cookies`) fails on Windows: the sent `Cookie:` header carries `partialip=nono` which the case does not expect (the case's precheck says it "only works for HOSTIPs ending with .0.0.1"; the cookie file names a partial IP domain). Compare with real curl 8.22.0 (`Record-CurlExchange.ps1`) and fix `Curl.Cookies.UnitLibrary` if Curl is wrong, with a focused unit test (ADR-0482).
- 777 (`--ssl-sessions` with weird sessions in file, `excluded | reference-lacks:ssl-sessions`) needs the `ssl-sessions` feature, absent from `UpstreamCurlPlatform` although Curl lists `SSLS-EXPORT` on Linux and macOS; align the harness's feature name with what Curl reports and run it.

Run each case with `$env:CURL_UPSTREAM_CASE='<n>'` as `Curl.Conformance.UnitTests/CLAUDE.md` describes. Where a fix lands outside the files in `touches`, add its folder in the same commit. If twelve cases prove too many for one run, finish 8, 777, 1703 and 472 first and file the disabled eight as a new task.

## Acceptance criteria

- [ ] Each of the twelve cases has its Windows outcome in Notes; every case that passes (ten of ten for a disabled one) is on `PassingUpstreamCases.txt`.
- [ ] Every case that is not listed has a one-sentence reason in Notes, and the disabled ones that stay off are named once in a new ADR "Decided by Claude under Stewart's delegation" with upstream's `DISABLED` reason.
- [ ] Case 8's cause is stated; if Curl was wrong, a focused unit test in `Curl.Cookies.UnitTests` fails without the fix.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 970768a5 bf92cf5e 722fd9db dd1cc079 620ebead.
