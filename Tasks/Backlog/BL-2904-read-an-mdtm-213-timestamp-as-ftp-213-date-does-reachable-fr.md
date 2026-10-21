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
completed:
---
# BL-2904 — Read an MDTM 213 timestamp as ftp_213_date does, reachable from the unit-program ports, for unit1668

## Goal

Curl reads a `213` MDTM timestamp as curl 8.22.0's `ftp_213_date` does, reachable from the unit-program ports, so the port of `tests/unit/unit1668.c` (BL-2845) passes.

## Context

`FtpModificationTime.Of` uses `DateTime.TryParseExact`, which refuses month 00, day 00 and 31 February; ftp_213_date accepts them (and fractions and trailing bytes) but refuses hour 25, minute 65, second 65 and any non-digit in the 14. Split the parse into the six fields the C function returns, and decide (ADR, measured against real curl with an MDTM reply) what `-R`/`-z` do with a date the BCL cannot hold. Source: https://github.com/curl/curl/blob/curl-8_22_0/lib/ftp.c and tests/unit/unit1668.c.

## Acceptance criteria

- [ ] An `ftp_213_date` counterpart gives every row of unit1668.c's table (result, and the six fields when true).
- [ ] `Curl.Conformance.UnitProgram.UnitLibrary` can call the counterpart (public, or `InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary"` in its library's csproj), so BL-2845 can port the program without touching this library.
- [ ] A focused unit test in the library's own `.UnitTests` project pins every row of the C program's table (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created (split from BL-2845, which waits on it).
