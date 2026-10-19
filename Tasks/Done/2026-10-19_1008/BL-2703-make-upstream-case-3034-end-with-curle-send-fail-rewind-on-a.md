---
id: BL-2703
title: Make upstream case 3034 end with CURLE_SEND_FAIL_REWIND on a redirected upload without a seek callback
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitTests/RedirectFollowerTests.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.InFileSize.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-19
---
# BL-2703 — Make upstream case 3034 end with CURLE_SEND_FAIL_REWIND on a redirected upload without a seek callback

## Goal

Upstream case 3034 (lib3034) passes through the runner and is on the ratchet.

## Context

Found by BL-2551, which ported lib3034 (`Curl.Conformance.LibTest.UnitLibrary/Lib3034.cs`): a verbose upload (`CURLOPT_UPLOAD`, `CURLOPT_INFILESIZE` 5, a `CURLOPT_READFUNCTION` and no `CURLOPT_SEEKFUNCTION`) with `CURLOPT_FOLLOWLOCATION`; the server answers `307` with `Location: /3034`, so real curl cannot rewind the body and ends the first transfer with `CURLE_SEND_FAIL_REWIND` (65); after `curl_easy_reset` a plain GET of the same URL succeeds and the program ends with 0. Curl hangs: `test3034 fails: curl did not finish within 20 seconds`. Find whether the first transfer or the transfer after the reset hangs (the engine's follow of a redirected upload with no seek callback, or a reset handle still holding the upload state), add the project at fault to `touches` before any edit, and run with `CURL_UPSTREAM_CASE=3034` and the `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` test.

## Acceptance criteria

- [x] Case 3034 passes through the runner on Windows and is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project covers the fix (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-19 (lane 2): case 3034 already passes through the runner on Windows (`CURL_UPSTREAM_CASE=3034`, `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`: "test3034 passes") and is on the ratchet since 11d9ca77 (BL-2645's fix in `CallbackReadStream`/`ManagedEasyTransfer`: a sized read-callback upload stops at `CURLOPT_INFILESIZE`, so the first transfer reaches the 307 and fails to rewind with 65). The hang was in the first transfer, never in the reset. Fixed library is `Curl.Engine.UnitLibrary`, not Libcurl.
- What is left: criterion 2 only. Add to `Curl.Engine.UnitTests/ManagedEasyTransferTests.InFileSize.cs` one focused test of 3034's shape: `Upload`, `InFileSize = 5`, a read callback that copies `"hello\0"` on every call, `FollowLocation`, no seek or ioctl callback, a `ScriptedConnector` answering `Continue + TemporaryRedirect` (as `PerformAsync_RedirectResendsAReadCallbackUploadAndSeekFails_...` in `ManagedEasyTransferTests.Callbacks.cs` does), under a 10-second cancellation; assert `CurlCode.SendFailRewind` and `CallbackReadStream.RewindImpossibleText`-based error text. Then build, fast tests, tick boxes, Done.
- Added `Curl.Engine.UnitTests/ManagedEasyTransferTests.InFileSize.cs` to `touches` for that test; BL-2714 (in Doing) holds the whole `Curl.Engine.UnitTests`, so requeued behind it.

- 2026-10-19 (lane 4): the focused engine test showed the 3034 shape still broke the engine: a sized read-callback upload (`CanSeek` because `DeclaredLength` is set) with no seek callback made `RedirectFollower.Rewind` throw `IOException` ("necessary data rewind wasn't possible") out of `PerformAsync`; case 3034 passed only because its program ends with the post-reset 0. Fixed in `Curl.Core.UnitLibrary/RedirectFollower.cs`: `SeekUploadToStart` turns that `IOException` into the hop's exit 65 with its message and curl's `-v` lines, through the same `UploadRewindFailure` the `IUploadRewinder` path uses. Added `RedirectFollower.cs` and `Curl.Core.UnitTests/RedirectFollowerTests.cs` to `touches` (no task in Doing named them). Tests: `FollowAsync_SeekableUploadThatCannotGoBack_EndsWithSendFailRewindAndTheStreamsMessage` (Core) and `PerformAsync_RedirectedSizedReadCallbackUploadWithoutASeekCallback_ReturnsSendFailRewind` (Engine). Measure-CodeQuality for Curl.Core.UnitLibrary: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 716b5cf3 48be2d2b 3d836d5e.
- 2026-10-19: Doing -> Backlog. Needs Curl.Engine.UnitTests/ManagedEasyTransferTests.InFileSize.cs, which BL-2714 (Doing) holds via the whole Curl.Engine.UnitTests; case 3034 already passes, only the focused engine test is left
- 2026-10-19: Backlog -> Doing. Criteria at claim: 716b5cf3 48be2d2b 3d836d5e.
- 2026-10-19: Doing -> Done. A redirected sized read-callback upload with no seek callback ends with exit 65 instead of throwing; case 3034 passes
