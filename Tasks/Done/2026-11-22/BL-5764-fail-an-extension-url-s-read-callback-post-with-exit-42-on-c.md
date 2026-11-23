---
id: BL-5764
title: Fail an extension URL's read-callback post with exit 42 on CURL_READFUNC_ABORT instead of sending a truncated body
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Mqtt.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Perform.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5764 — Fail an extension URL's read-callback post with exit 42 on CURL_READFUNC_ABORT instead of sending a truncated body

## Goal

A `Curl.Api` stream body (`CURLOPT_READFUNCTION` post) to an extension URL whose read callback answers `CURL_READFUNC_ABORT` ends the transfer with `CURLE_ABORTED_BY_CALLBACK` (exit 42) instead of reaching the handler as a truncated or empty body.

## Context

- `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:292` (`ReadAllOf`, added by BL-5754 / ADR-1337 this shift) stops reading on `count <= 0 || count > wanted` and returns what it has. `CURL_READFUNC_ABORT` (0x10000000) is `count > wanted`, so an abort mid-body gives the handler the bytes read so far and the transfer goes on to succeed. `CURL_READFUNC_PAUSE` is not a plain zero either, and is also taken as the end of the body.
- Everywhere else the engine maps a read-callback abort to `CurlCode.AbortedByCallback` (see `ManagedEasyTransfer.Finish.cs`), as libcurl does.
- The new test `TransferSettingsOf_ReadCallbackAnsweringMoreThanAskedOnAnExtensionUrl_StopsReading` (`Curl.Engine.UnitTests/ManagedEasyTransferTests.Mqtt.cs:70`) pins the empty body for 0x10000000, which locks the bug in.
- Found by the shift-end review of 2026-11-22 (pr-review-toolkit code-reviewer, review-20261122-134350).

## Acceptance criteria

- [x] A read callback that returns `CURL_READFUNC_ABORT` while an extension URL's post data is read up front makes the transfer end with `CurlCode.AbortedByCallback` (42), and no handler runs with a partial body.
- [x] `CURL_READFUNC_PAUSE` during that read is handled explicitly (decided and recorded in ADR-1337 or a new ADR), not treated as the end of the body.
- [x] A count larger than asked that is not a control code still ends the read as today, or fails as libcurl does (`CURLE_READ_ERROR`), whichever matches libcurl; the test at `ManagedEasyTransferTests.Mqtt.cs:70` is replaced to pin the new behaviour.
- [x] Curl.Engine.UnitLibrary stays at 100% line and branch coverage; `dotnet build` is clean and the fast tests pass.

## Notes

Added ManagedEasyTransfer.Perform.cs to touches: the perform must catch the read failure; no other Doing task touches it. PAUSE decided in ADR-1339 (CURLE_READ_ERROR). Quality run for Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 5a3940a1 d5643ed9 88a63bc1 dd656b97.
- 2026-11-22: Doing -> Done. Abort, pause and funny read values on an extension URL's read-callback post now fail the transfer (ADR-1339)
