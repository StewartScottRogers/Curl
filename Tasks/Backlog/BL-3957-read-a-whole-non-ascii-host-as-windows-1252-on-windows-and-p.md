---
id: BL-3957
title: Read a whole non-ASCII host as Windows-1252 on Windows and pin the unit tests to a locale (CGR-059)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitTests/Cgr059Windows1252HostTests.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHostNameMapping.cs, Curl.Protocol.Abstractions.UnitTests/Cgr059PercentEncodedHostTests.cs, Curl.Protocol.Abstractions.UnitTests/Cgr059BidiTests.cs, Curl.Protocol.Abstractions.UnitTests/Cgr059LabelRulesTests.cs, Curl.Protocol.Abstractions.UnitTests/Cgr019Tests.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3957 — Read a whole non-ASCII host as Windows-1252 on Windows and pin the unit tests to a locale (CGR-059)

## Goal

On Windows, Curl converts a non-ASCII host name from bytes read as Windows-1252, every byte and not only those that are not UTF-8, as curl 8.22.0 does, and every unit test in `Curl.Protocol.Abstractions.UnitTests` passes on each platform.

## Context

First part of BL-3953 (CGR-059). `CurlUrlHost.cs` (`StrayBytesAsWindows1252`, `ReadStrayBytesAsWindows1252`) reads only bytes that are not valid UTF-8 as Windows-1252. Real curl 8.22.0 on Windows reads the whole host in the ANSI code page: `http://%C3%BC.example:20200/a` gives `Host: xn--14-ria7423a.example:20200` (exit 0) and `http://a%D7%90.example:20200/a` exits 3 (`URL using bad/illegal format or missing URL`; 0x90 is undefined in Windows-1252). `%F0%90%82%80a.example` also exits 3. Linux (UTF-8 locale) stays as it is: `Cgr059BidiTests` and `Cgr059PercentEncodedHostTests` pin it with `strayBytesAsWindows1252: false`. The stashed work renames `TryNormalizeReadingStrayBytes` to `TryNormalizeReadingHost(hostAsWindows1252:)`, adds `ReadHostAsWindows1252` (each typed character becomes its Windows-1252 byte, or its UTF-8 when Windows-1252 lacks it, each escape its raw byte, then every byte is read back as Windows-1252) and `Latin1Mappings` in `CurlUrlHostNameMapping` (`¼` to `1⁄4`). Still failing after it, to pin to `hostAsWindows1252: false` (UTF-8 locale) or give per-platform answers: 6 rows of `Cgr019Tests.TryNormalize_HostThatIsNotAscii_ConvertsTheMappedName` (ΣΣ, İstanbul, Ａb, a‍b, NFD école, ﬁle) and `CurlUrlTests.TryParse_ForEachScheme_ExposesTheMembersHandlersRead` (`ex%C3%A5mple`). The old `%F0%90%82%80a` test in `Cgr059PercentEncodedHostTests` was wrong (real curl refuses it). Does not cover test1448 (BL-3958) or the ADR (BL-3959).

## Acceptance criteria

- [ ] On Windows, `http://%C3%BC.example:20200/a` and `http://a%D7%90.example:20200/a`, put as `url = "..."` in a UTF-8 config file (`-sS -K`) with `--connect-to ::127.0.0.1:20200` and run through real curl (`/ucrt64/bin/curl`) and Curl with Record-CurlExchange.ps1, give the same exit code, stdout bytes, stderr text and request bytes; the commands and results are recorded in Notes.
- [ ] A unit test in `Cgr059Windows1252HostTests.cs` pins the Windows-1252 reading of valid UTF-8 bytes (`%C3%BC` to `xn--14-ria7423a`, `a%D7%90` refused, `%F0%90%82%80a` refused) and fails before the fix.
- [ ] `Cgr059BidiTests`, `Cgr059PercentEncodedHostTests`, `Cgr059LabelRulesTests`, `Cgr019Tests` and `CurlUrlTests` pass on Windows and are platform-neutral (pinned to `hostAsWindows1252: false` or one answer per platform).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3953, which went over its cost cap.
- Continues the stashed work of BL-3953.

## Log

- 2026-10-28: Created.
