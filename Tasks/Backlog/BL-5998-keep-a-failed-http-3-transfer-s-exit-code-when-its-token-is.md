---
id: BL-5998
title: Keep a failed HTTP/3 transfer's exit code when its token is cancelled while its stream is cancelled
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ConnectionExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseExchange.cs, Curl.Original.Protocol.Http.UnitTests/Http3FailedTransferStreamTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5998 — Keep a failed HTTP/3 transfer's exit code when its token is cancelled while its stream is cancelled

## Goal

An HTTP/3 transfer that `-f`, `--max-filesize`, a failed write or a server reset ends keeps its own exit code (22, 63, 23, or the reset's), its partial report and its transfer-done bookkeeping, even when its cancellation token fires while its stream is being cancelled.

## Context

Found by the shift-end review of 2026-11-25 (ADR-0710; `pr-review-toolkit:silent-failure-hunter`, confirmed by `type-design-analyzer`) in the diff `1b6ff90a3e...e2964660f1` (BL-5983, BL-5996, BL-5997).

- `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseExchange.cs:108`: the `catch (HttpTransferException failure)` block calls `ResetFailedStreamAsync(..., cancellationToken)` with the transfer's own token. The timeout path two lines below already uses `CancellationToken.None`.
- That path now does real network work over HTTP/3: `ResetFailedStreamAsync` → `EndGivenUpStreamAsync` → `Http3StreamConnection.CancelUnlessEndedBothWaysAsync` (`Http3StreamConnection.cs:237`) → `Http3ClientRequestStream.CancelAsync` → `Http3ClientSession.CancelRequestStreamAsync`. That last call waits on `writingDecoderStream.WaitAsync(cancellationToken)` and writes to the decoder stream.
- `CancelUnlessEndedBothWaysAsync` catches only `IOException`. If the token is cancelled during that work (the `-m` limit or the `-Y/-y` watchdog firing just as a 404 arrives), an `OperationCanceledException` escapes the catch block. The sibling `catch (OperationCanceledException)` clauses do not see an exception thrown from inside a catch block.
- What goes wrong:
  - the original failure's exit code is replaced by a cancellation;
  - `AddCancelledStream` and `ReportTransferDone` are skipped, so the `quic close` trace line is lost and `Http3Session.activeRequestStreams` stays raised;
  - `CancelledReportDataKey` is never set.

## Acceptance criteria

- [ ] The stream cleanup in the `HttpTransferException` path of `HttpProtocolHandler.ResponseExchange.cs` runs under `CancellationToken.None` (or catches the cleanup's `OperationCanceledException`), so the transfer's `HttpTransferException` outcome is what is returned.
- [ ] A unit test in `Curl.Original.Protocol.Http.UnitTests` fails an HTTP/3 transfer with `-f` (exit 22) using a token cancelled before the stream cancel completes. It asserts exit 22 and that the transfer was marked done with the session.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, with `Curl.Original.Protocol.Http.UnitLibrary` still at 100% line and branch coverage.

## Notes

## Log

- 2026-11-25: Created.
