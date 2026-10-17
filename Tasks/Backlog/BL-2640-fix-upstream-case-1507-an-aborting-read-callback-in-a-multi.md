---
id: BL-2640
title: Fix upstream case 1507: an aborting read callback in a multi SMTP upload ends the transfer with 42, not an exception
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2640 — Fix upstream case 1507: an aborting read callback in a multi SMTP upload ends the transfer with 42, not an exception

## Goal

Upstream case 1507 passes through the runner and holds the ratchet.

## Context

Found by BL-2537, which ported lib1507 (`Curl.Conformance.LibTest.UnitLibrary/Lib1507.cs`) and unit tested it over a fake engine; the port matches upstream's C, so the difference is in the engine or Libcurl. The program runs a verbose SMTP upload (`CURLOPT_MAIL_FROM`, `CURLOPT_MAIL_RCPT`) through a multi handle whose read callback answers `CURL_READFUNC_ABORT` at once; in curl the transfer ends with 42 (`CURLE_ABORTED_BY_CALLBACK`) through `curl_multi_info_read` and the program exits 0. The runner reports: `test1507 fails: curl threw IOException: operation aborted by callback` - the abort escapes `curl_multi_perform` as an exception instead of ending the transfer with its code. Look at the multi transfer's perform path and how the SMTP upload's read source maps a read-callback abort; find the project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [ ] test1507 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; if it still fails it is filed with the board script with its differing lines.
- [ ] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
