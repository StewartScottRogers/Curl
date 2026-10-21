---
id: BL-2896
title: Add a Curl_get_line counterpart (curl's file line reader) the unit-program ports reach, for unit3200
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/CurlFileLineReader.cs, Curl.Core.UnitTests/CurlFileLineReaderTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2896 — Add a Curl_get_line counterpart (curl's file line reader) the unit-program ports reach, for unit3200

## Goal

Curl has a counterpart of curl 8.22.0's `Curl_get_line` (the line reader for cookie, alt-svc, HSTS and netrc files), so the port of `tests/unit/unit3200.c` (BL-2845) passes.

## Context

unit3200.c writes six files to the path its command names and reads them line by line: a last line without a newline gets one, a line longer than 4096 bytes is dropped (with the rest of the file when it is last), and a `^Z` byte is kept. Check whether `AltSvcFileLineParser`, `HstsFileLineParser`, `NetrcFile` or the cookie reader already behave so; where one differs, that is a fix with its own test (and its file in `touches`). Source: https://github.com/curl/curl/blob/curl-8_22_0/lib/curl_get_line.c.

## Acceptance criteria

- [ ] A `Curl_get_line` counterpart reading a `Stream` gives every case of unit3200.c (line text, end of file, over-long line dropped).
- [ ] `Curl.Conformance.UnitProgram.UnitLibrary` can call the counterpart (public, or `InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary"` in its library's csproj), so BL-2845 can port the program without touching this library.
- [ ] A focused unit test in the library's own `.UnitTests` project pins every row of the C program's table (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created (split from BL-2845, which waits on it).
