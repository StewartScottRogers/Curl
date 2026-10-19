---
id: BL-2787
title: Make upstream cases 569 and 571 pass: CURLOPT_RTSP_SESSION_ID NULL forgets the session, and RTSP interleave finishes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Api.UnitLibrary, Curl.Api.UnitTests, Curl.Engine.UnitLibrary/HandleRtspSession.cs, Curl.Engine.UnitTests/HandleRtspSessionTests.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Wildcard.cs, Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed: 2026-10-19
---

## Goal

Upstream cases 569 and 571 pass through the runner and join `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Left by BL-2704, which carries the `CURLOPT_RTSP_*` options into the RTSP handler (`Curl.Engine.UnitLibrary/HandleRtspSession.cs`). Measured on Windows, 2026-10-18:

- test569: `<verify><protocol>` line 11, expected `Transport: Fake/NotReal/JustATest;foo=baz`, got `Session: 00.+1-am-aSe55ion_id\$yes-i-am\$`. `lib569` sets `CURLOPT_RTSP_SESSION_ID` to NULL after each TEARDOWN; libcurl forgets the session then. `EasyRtspOptions` is a record, so a NULL set over a NULL option is invisible to the engine, which hands an ID on only when the option changes. Needs a per-setopt signal (for example a set count on `EasyRtspOptions` that `Libcurl`'s `SetRtspSessionId` bumps, read by `HandleRtspSession`, which calls `RtspSessionState.ForgetSessionId`).
- test571: curl did not finish within 20 seconds (SETUP / PLAY / RECEIVE with `CURLOPT_INTERLEAVEFUNCTION`); find where the RECEIVE or interleaved read waits.

Debug with `CURL_UPSTREAM_CASE=<n>` and `dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Manual"`.

## Acceptance criteria

- [x] test569 and test571 pass through the runner on Windows and are on `PassingUpstreamCases.txt`, or one that still fails is filed with its differing lines.
- [x] Each fix has a focused unit test in its library's `.UnitTests` project (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- test569: `EasyRtspOptions.SessionIdSetCount` counts every `CURLOPT_RTSP_SESSION_ID` set (Libcurl's `SetRtspSessionId` bumps it); `HandleRtspSession` hands the ID on, and forgets the state's ID for NULL, when the value or the count changed, so a NULL set after a NULL forgets the session as libcurl's setopt does.
- test571 had three causes, all in `Curl.Protocol.Rtsp.UnitLibrary`: (1) a `RECEIVE` read until the server closed; now it takes one read and stops unless a frame is left incomplete, as `rtsp_rtp_write_resp` sets `download_done`. (2) Frames after a reply's body were dropped with `RtspReplyHead.Remaining`; now `TakeBytesAfterReplyAsync` hands them to the sink and keeps a cut-off frame (or a reply's start) in `RtspSessionState.CarriedBytes` for the connection's next request, forgotten on a new connection. (3) The `size_err -6` frame leaves `$_1234` on the wire, which the filter read as a 12594-byte frame on channel `_` and waited for; libcurl's `rtsp_filter_rtp` only takes `$` on a channel a reply's `Transport: ...interleaved=a-b` set up (`rtp_channel_mask`, `rtsp_parse_transport`), so `RtspSessionState.AcceptTransport` and `IsChannelSetUp` do the same and the filter treats any other `$` as a stray byte.
- Choice: matched libcurl exactly, so a frame before any `Transport` reply is skipped even with an interleave callback; existing tests that sent frames with no `Transport` now set channels up first (RTSP tests through a session state, the two `ManagedEasyTransferTests.Wildcard.cs` tests through a reply with `Transport` and the frame after it, renamed `...AfterTheReply...`). `rtsp_parse_transport`'s `infof` line for an unreadable `interleaved=` value is not written (no case checks it).
- touches widened (no other task in Doing on origin/work/dark-factory): `Curl.Api.UnitTests` (`EasyRtspOptionsTests` counts the record's properties), `Libcurl.UnitTests` (focused setter test) and `Curl.Engine.UnitTests/ManagedEasyTransferTests.Wildcard.cs` (the two interleave tests above).
- Measured: test569 and test571 pass through the runner on Windows; full fast run green; Measure-CodeQuality for Curl.Protocol.Rtsp.UnitLibrary: 0 failing members. Neither case involves a path, OS error or certificate, so both are platform-neutral.

## Log

- 2026-10-18: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 42d7ebda fb1216c9 4272a6b9.
- 2026-10-19: Doing -> Done. Upstream cases 569 and 571 pass and are on the ratchet: a NULL CURLOPT_RTSP_SESSION_ID set forgets the session, and RTSP interleave handles reply Transport channels, frames after a reply and one-read RECEIVE
