---
id: BL-3013
title: Bring Curl.Networking.UnitLibrary's DoH decoder and trace files back to 100% coverage after the 8.22.0 change
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3011]
touches: [Curl.Networking.UnitLibrary/DnsAnswerReader.cs, Curl.Networking.UnitLibrary/DnsAnswerReader.AnswerRecord.cs, Curl.Networking.UnitLibrary/DnsAnswerReader.CanonicalName.cs, Curl.Networking.UnitLibrary/DnsAnswerReader.Name.cs, Curl.Networking.UnitLibrary/DohQueryResult.cs, Curl.Networking.UnitLibrary/DohTraceLines.cs, Curl.Networking.UnitLibrary/DohTraceLines.AnswerDecoding.cs, Curl.Networking.UnitLibrary/DohDnsResolver.SubTransferTrace.cs, Curl.Networking.UnitTests/DohDnsResolverTraceTests.AnswerDecoding.cs, Curl.Networking.UnitTests/DnsAnswerDecoderTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-22
---
# BL-3013 — Bring Curl.Networking.UnitLibrary's DoH decoder and trace files back to 100% coverage after the 8.22.0 change

## Goal

`Curl.Networking.UnitLibrary` keeps 100% line and branch coverage, complexity at most 10 and CRAP at most 30 after BL-3011's decoder and trace-line change, with any gap closed by focused tests.

## Context

Split from BL-2970. BL-3011 lands the code and its tests but does not run the quality measurement. Run `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` once (the script's header gives the options) and read the members it lists. Likely places: `DnsAnswerDecoder` (the skipped CNAME and DNAME branch), `DohQueryResult` (the room rule without CNAMEs), `DohTraceLines.FailureLine` and `DohDnsResolver.SubTransferTrace.ReportOutcome`. Add the missing tests to `Curl.Networking.UnitTests/DnsAnswerDecoderTests.cs` or `DohDnsResolverTraceTests.AnswerDecoding.cs`; if a gap lies in another file, add that file to `touches` in the same commit. Never change `CodeMetricsConfig.txt`.

## Acceptance criteria

- [x] `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports no member outside the four gates (line, branch, complexity at most 10, CRAP at most 30).
- [x] Each test added fails when the line or branch it covers is removed, and is named after the behaviour it pins.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Networking.UnitTests`.

## Notes

- 2026-10-22: Measured `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line, 100% branch, 1610 members, 0 failing, worst CRAP 10. BL-3011's tests already cover the decoder and trace-line change, so no test was needed; the second criterion holds vacuously (no test added). `dotnet build Curl.slnx -warnaserror` clean (0 warnings); Curl.Networking.UnitTests fast tests 3356 passed, 29 skipped, 0 failed.

## Log

- 2026-10-21: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 5b56393b b4737a67 604ae9a6.
- 2026-10-22: Doing -> Done. Curl.Networking.UnitLibrary measured at 100% line and branch with no member outside the gates after BL-3011
