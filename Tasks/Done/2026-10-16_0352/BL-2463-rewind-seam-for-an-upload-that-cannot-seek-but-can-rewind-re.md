---
id: BL-2463
title: Rewind seam for an upload that cannot seek but can rewind (redirect, auth retry)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2463 — Rewind seam for an upload that cannot seek but can rewind (redirect, auth retry)

## Goal

An upload stream that cannot seek (`CanSeek` false, no `Length`) but can be rewound to its start is rewound by `RedirectFollower` before a followed redirect that resends the body and by the HTTP handler before an authentication retry, a 417 resend or a refused-stream retry, and a rewind that fails ends the transfer with exit 65 (`CURLE_SEND_FAIL_REWIND`) and the rewinder's own message, as upstream curl 8.22.0's `cr_in_rewind` does.

## Context

Prerequisite of BL-2417 (libcurl's `CURLOPT_SEEKFUNCTION`). Today the only rewind is `Stream.Position = start`, taken only when `CanSeek` is true (`Curl.Core.UnitLibrary/RedirectFollower.cs` `SeekableStart`/`Rewind`; `Curl.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs` `StartReading`/`Rewound`; `HttpProtocolHandler.TryRewindUpload` and `RewindForResend`). The engine's `CallbackReadStream` (the libcurl read callback) cannot claim `CanSeek`: a dozen handlers then read `Length - Position` for a size (`HttpUploadResume.Of`, `FtpSession`, `SmtpMailTransaction`, `ScpFileUpload`, `SftpFileUpload`, `FileProtocolHandler`), and the callback's size is unknown. So add a small interface in `Curl.Protocol.Abstractions` (pick a name that does not clash with Http's existing `IRewindableUpload.CanRewind`), e.g. a method that rewinds to the start and gives `null` on success or the failure message, which `RedirectFollower` and the HTTP handler call when the upload implements it and some of it was read (upstream rewinds nothing when the read callback was never called: `cr_in_rewind` checks `read_len`). A failure becomes `TransferResult.Failure(CurlExitCode.SendFailRewind, message)` with the earlier hop's report, and under `-v` the message is an info line followed by `rewind of client reader 'cr-in' failed: 65` (`HttpTransferMessages.UploadReaderRewindFailed`). Seekable streams keep their current `Position` path unchanged. BL-2417 then makes `CallbackReadStream` implement the interface over `CURLOPT_SEEKFUNCTION` (`seek(0, SEEK_SET)`; any non-zero `SeekResult` is `seek callback returned error <n>`).

## Acceptance criteria

- [x] `Curl.Core.UnitTests` has a test where a 307 redirect rewinds a non-seekable rewindable upload once and resends it whole, one where the rewind fails and the transfer ends with `CurlExitCode.SendFailRewind` and the rewinder's message, and one where nothing was read so no rewind is asked for.
- [x] `Curl.Protocol.Http.UnitTests` has a test where an authentication retry (401 answered under `--anyauth` or Digest) rewinds such an upload, and one where its failed rewind ends with exit 65.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary,Curl.Core.UnitLibrary,Curl.Protocol.Http.UnitLibrary` reports 0 failing members.
- [x] Every new public member has an XML doc comment, and the touched projects' CLAUDE.md files stay true.

## Notes

- Filed by BL-2417's lane run (2026-10-15), which found the engine alone cannot rewind a read callback.
- Seam: `Curl.Protocol.Abstractions.IUploadRewinder` (`HasBeenRead`, `RewindToStart()` giving null or the failure message, `ReaderRewindFailedLine`); decisions in ADR-0592. `HttpRequestPlan.UploadCannotRewind` became `UploadRewindFailure` (a message), set on auth, proxy-auth, 417 and refused-stream retries from `HttpRequestBodyWriter.RewindFailure`; the same-connection retry loop in `ExchangeWithRetriesAsync` checks it too, since it skips `FailureBeforeConnecting`. `MayRetry` now lets a rewinder upload answer a 401/407.
- Tests: `RedirectFollowerTests.FollowAsync_RewinderUpload*` (3) and `HttpProtocolHandlerTests.ExecuteAsync_ChallengeToABodyThatRewindsThroughItsRewinder*`, `..WhoseRewinderFails*`, `..ChallengeBeforeARewinderBodyIsRead*`. A body of unknown length waits for 100-continue, so the 401 tests use a 5-byte body to be read before the challenge.
- Measured 2026-10-16: Measure-CodeQuality for the three libraries, 100/100, 0 failing members; fast tests green; PassingUpstreamCases.txt unchanged.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-16: Doing -> Done. An upload that cannot seek but implements IUploadRewinder is rewound on redirects and HTTP retries; a failed rewind ends with exit 65 and the rewinder's message
