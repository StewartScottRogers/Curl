---
id: BL-2640
title: Fix upstream case 1507: an aborting read callback in a multi SMTP upload ends the transfer with 42, not an exception
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2640 — Fix upstream case 1507: an aborting read callback in a multi SMTP upload ends the transfer with 42, not an exception

## Goal

Upstream case 1507 passes through the runner and holds the ratchet.

## Context

Found by BL-2537, which ported lib1507 (`Curl.Conformance.LibTest.UnitLibrary/Lib1507.cs`) and unit tested it over a fake engine; the port matches upstream's C, so the difference is in the engine or Libcurl. The program runs a verbose SMTP upload (`CURLOPT_MAIL_FROM`, `CURLOPT_MAIL_RCPT`) through a multi handle whose read callback answers `CURL_READFUNC_ABORT` at once; in curl the transfer ends with 42 (`CURLE_ABORTED_BY_CALLBACK`) through `curl_multi_info_read` and the program exits 0. The runner reports: `test1507 fails: curl threw IOException: operation aborted by callback` - the abort escapes `curl_multi_perform` as an exception instead of ending the transfer with its code. Look at the multi transfer's perform path and how the SMTP upload's read source maps a read-callback abort; find the project at fault and add it to `touches` before any edit.

## Acceptance criteria

- [x] test1507 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; if it still fails it is filed with the board script with its differing lines.
- [x] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: SMTP's `SmtpMailTransaction.SendAsync` does not catch the plain `IOException` `CallbackReadStream` throws on `CURL_READFUNC_ABORT`, so it escaped `ManagedEasyTransfer.FollowUnlessAbortedAsync` and out of `curl_multi_perform`. Fix in `Curl.Engine.UnitLibrary`: catch `IOException` when a read stream `Aborted` and end the perform with 42 like the other abort paths. Chose the engine over changing the stream's exception to `RequestBodyReadFailedException`, which the HTTP body writer and the multipart builder treat as a read error (exit 26), so HTTP behaviour stays as it is.
- Touches widened to Curl.Engine.UnitLibrary and Curl.Engine.UnitTests: no other task was in Doing on origin/work/dark-factory.
- Test: `ManagedEasyTransferTests.PerformAsync_SmtpUploadWhoseReadCallbackAborts_ReturnsAbortedByCallbackAfterDataWithoutThrowing`. The new catch filter pushed `FollowUnlessAbortedAsync` to complexity 12; extracted `AnyReadAborted`, now 0 failing members in Measure-CodeQuality for Curl.Engine.UnitLibrary.
- test1507 is SMTP only, no paths or OS text, so it is platform-neutral on the ratchet.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: e0d6c1bf c59fcab3 3d836d5e.
- 2026-10-18: Doing -> Done. An aborting read callback in a multi SMTP upload ends the transfer with 42; test1507 holds the ratchet
