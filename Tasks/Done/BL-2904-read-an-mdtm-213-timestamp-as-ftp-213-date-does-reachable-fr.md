---
id: BL-2904
title: Read an MDTM 213 timestamp as ftp_213_date does, reachable from the unit-program ports, for unit1668
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpModificationTime.cs, Curl.Protocol.Ftp.UnitLibrary/Curl.Protocol.Ftp.UnitLibrary.csproj, Curl.Protocol.Ftp.UnitTests/FtpModificationTimeTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2904 — Read an MDTM 213 timestamp as ftp_213_date does, reachable from the unit-program ports, for unit1668

## Goal

Curl reads a `213` MDTM timestamp as curl 8.22.0's `ftp_213_date` does, reachable from the unit-program ports, so the port of `tests/unit/unit1668.c` (BL-2845) passes.

## Context

`FtpModificationTime.Of` uses `DateTime.TryParseExact`, which refuses month 00, day 00 and 31 February; ftp_213_date accepts them (and fractions and trailing bytes) but refuses hour 25, minute 65, second 65 and any non-digit in the 14. Split the parse into the six fields the C function returns, and decide (ADR, measured against real curl with an MDTM reply) what `-R`/`-z` do with a date the BCL cannot hold. Source: https://github.com/curl/curl/blob/curl-8_22_0/lib/ftp.c and tests/unit/unit1668.c.

## Acceptance criteria

- [x] An `ftp_213_date` counterpart gives every row of unit1668.c's table (result, and the six fields when true).
- [x] `Curl.Conformance.UnitProgram.UnitLibrary` can call the counterpart (public, or `InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary"` in its library's csproj), so BL-2845 can port the program without touching this library.
- [x] A focused unit test in the library's own `.UnitTests` project pins every row of the C program's table (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- `FtpModificationTime.TryReadDate(ReadOnlySpan<char>, out FtpMdtmDate)` is the `ftp_213_date` counterpart (internal; the csproj grants `InternalsVisibleTo` to `Curl.Conformance.UnitProgram.UnitLibrary`, and to `Curl.Protocol.Ftp.UnitTests`, which had none). The C program's `\xNN` bytes map to the char of the same value (Latin-1) in the tests; BL-2845's port should map them the same way.
- `Of` now dates the fields as `Curl_getdate_capped` does (ADR-0725): 31 February, day 00 and second 60 roll over, month 00 and years before 1583 are unknown. Measured with `Record-CurlExchange.ps1 -Ftp -FtpReply 'MDTM=213 <date>' -CurlArgs -sS,-I,...` against curl 8.22.0 (Schannel): 19980231114234 -> 03 Mar 1998 11:42:34, 19980300114234 -> 28 Feb, 19980320115960 -> 12:00:00, 19980020114234 and 09980320114234 -> no Last-Modified.
- Default taken: a rollover past `DateTimeOffset.MaxValue` (99991231235960) is unknown, recorded in ADR-0725.
- Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary: 100% line, 100% branch, 0 failing members, worst CRAP 10 (the range check was split out of `TryReadDate`, which first measured at complexity 14).

## Log

- 2026-10-21: Created (split from BL-2845, which waits on it).
- 2026-10-21: Backlog -> Doing. Criteria at claim: 54b456c3 3d228e1f d3a9ab15 099d1d27.
- 2026-10-21: Doing -> Done. FtpModificationTime.TryReadDate reads every unit1668 row as ftp_213_date does, reachable from the unit-program ports; Of rolls over odd MDTM dates as curl does (ADR-0725)
