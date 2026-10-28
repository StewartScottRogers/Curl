---
id: BL-3963
title: Read a whole non-ASCII host as Windows-1252 on Windows and pin the unit tests to a locale (CGR-059)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitTests/Cgr059Windows1252HostTests.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHostNameMapping.cs, Curl.Protocol.Abstractions.UnitTests/Cgr059PercentEncodedHostTests.cs, Curl.Protocol.Abstractions.UnitTests/Cgr059BidiTests.cs, Curl.Protocol.Abstractions.UnitTests/Cgr059LabelRulesTests.cs, Curl.Protocol.Abstractions.UnitTests/Cgr019Tests.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3963 — Read a whole non-ASCII host as Windows-1252 on Windows and pin the unit tests to a locale (CGR-059)

## Goal

On Windows, Curl converts a non-ASCII host name from bytes read as Windows-1252, every byte and not only those that are not UTF-8, as curl 8.22.0 does, and every unit test in `Curl.Protocol.Abstractions.UnitTests` passes on each platform.

## Context

First part of BL-3953 (CGR-059). `CurlUrlHost.cs` (`StrayBytesAsWindows1252`, `ReadStrayBytesAsWindows1252`) reads only bytes that are not valid UTF-8 as Windows-1252. Real curl 8.22.0 on Windows reads the whole host in the ANSI code page: `http://%C3%BC.example:20200/a` gives `Host: xn--14-ria7423a.example:20200` (exit 0) and `http://a%D7%90.example:20200/a` exits 3 (`URL using bad/illegal format or missing URL`; 0x90 is undefined in Windows-1252). `%F0%90%82%80a.example` also exits 3. Linux (UTF-8 locale) stays as it is: `Cgr059BidiTests` and `Cgr059PercentEncodedHostTests` pin it with `strayBytesAsWindows1252: false`. The stashed work renames `TryNormalizeReadingStrayBytes` to `TryNormalizeReadingHost(hostAsWindows1252:)`, adds `ReadHostAsWindows1252` (each typed character becomes its Windows-1252 byte, or its UTF-8 when Windows-1252 lacks it, each escape its raw byte, then every byte is read back as Windows-1252) and `Latin1Mappings` in `CurlUrlHostNameMapping` (`¼` to `1⁄4`). Still failing after it, to pin to `hostAsWindows1252: false` (UTF-8 locale) or give per-platform answers: 6 rows of `Cgr019Tests.TryNormalize_HostThatIsNotAscii_ConvertsTheMappedName` (ΣΣ, İstanbul, Ａb, a‍b, NFD école, ﬁle) and `CurlUrlTests.TryParse_ForEachScheme_ExposesTheMembersHandlersRead` (`ex%C3%A5mple`). The old `%F0%90%82%80a` test in `Cgr059PercentEncodedHostTests` was wrong (real curl refuses it). Does not cover test1448 (BL-3964) or the ADR (BL-3965).

## Acceptance criteria

- [x] On Windows, `http://%C3%BC.example:20200/a` and `http://a%D7%90.example:20200/a`, put as `url = "..."` in a UTF-8 config file (`-sS -K`) with `--connect-to ::127.0.0.1:20200` and run through real curl (`/ucrt64/bin/curl`) and Curl with Record-CurlExchange.ps1, give the same exit code, stdout bytes, stderr text and request bytes; the commands and results are recorded in Notes.
- [x] A unit test in `Cgr059Windows1252HostTests.cs` pins the Windows-1252 reading of valid UTF-8 bytes (`%C3%BC` to `xn--14-ria7423a`, `a%D7%90` refused, `%F0%90%82%80a` refused) and fails before the fix.
- [x] `Cgr059BidiTests`, `Cgr059PercentEncodedHostTests`, `Cgr059LabelRulesTests`, `Cgr019Tests` and `CurlUrlTests` pass on Windows and are platform-neutral (pinned to `hostAsWindows1252: false` or one answer per platform).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3953, which went over its cost cap.
- Continues the stashed work of BL-3953.
- Measured on Windows, 2026-10-28, with `Record-CurlExchange.ps1 -Port 20288 [-Curl Curl.Console\bin\Debug\net10.0\curl.exe] -CurlArgs '-sS','-K','u.cfg','--connect-to','::127.0.0.1:20288'`, `u.cfg` holding one UTF-8 line `url = "http://<host>.example:20288/a"` (port 20288, not 20200: 20200 was held by another process on the machine). Real curl (`/ucrt64/bin/curl`, 8.22.0) and Curl agreed byte for byte (exit code, stdout, stderr, request):
  - `%C3%BC`: exit 0, empty stdout and stderr, request `Host: xn--14-ria7423a.example:20288`.
  - `a%D7%90`: exit 3, stderr `curl: (3) URL using bad/illegal format or missing URL`, nothing sent.
- `Cgr059Windows1252HostTests` fails before the fix: `TryNormalizeReadingHost(hostAsWindows1252:)` did not exist and the old reading kept valid UTF-8 as UTF-8.
- `Cgr019Tests`, `Cgr059BidiTests`, `Cgr059LabelRulesTests` and `Cgr059PercentEncodedHostTests` call `TryNormalizeReadingHost` with `hostAsWindows1252: false` (the UTF-8 locale answer); `CurlUrlTests`' `ex%C3%A5mple` row is now typed `exåmple`, which reads the same in both, so each test has one answer on every platform. CI on Linux and macOS: met by keeping these tests platform-neutral and the fast tests green here.
- Quality gate (`Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary`) first found `CurlUrlHostNameMapping.MapCharacter` at complexity 12 and a typed U+0080 to U+009F branch of `CurlUrlHost.Windows1252ByteOf` untested; split `MapCharacterOutsideTheRanges` and `Windows1252ByteFrom80To9FOf` out and added an `a\u0085` row; the second run reports 0 failing members.
- test1448 is off `PassingUpstreamCases.txt` until BL-3964 (recorded in that task); the ADR is BL-3965.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 11e178b8 645c13da 981daa30 779790c0.
- 2026-10-28: Doing -> Done. Windows reads a whole non-ASCII host as Windows-1252, matching curl 8.22.0; tests pinned per locale
