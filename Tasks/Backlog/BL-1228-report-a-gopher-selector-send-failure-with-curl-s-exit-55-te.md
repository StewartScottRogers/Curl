---
id: BL-1228
title: Report a gopher selector send failure with curl's exit 55 texts and its 'Failed sending Gopher request' -v line
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Gopher.UnitLibrary, Curl.Protocol.Gopher.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1228 — Report a gopher selector send failure with curl's exit 55 texts and its 'Failed sending Gopher request' -v line

## Goal

A `gopher://` or `gophers://` transfer whose selector cannot be sent ends with exit 55 and curl 8.21.0's message (`Send failure: Connection was reset` for a reset, `Failed sending data to the peer` otherwise) and writes curl's `Failed sending Gopher request` `-v` line before the connection-end line.

## Context

- Today `Curl.Protocol.Gopher.UnitLibrary/GopherProtocolHandler.cs` `TrySendAsync` swallows the `IOException` and returns `false`, and `ExchangeAsync` returns exit 55 with `GopherTransferMessages.SendFailed`, `Failure when sending data to the peer`. That text is no curl message: `curl_easy_strerror(CURLE_SEND_ERROR)` is `Failed sending data to the peer` (`lib/strerror.c` lines 177-178 at `curl-8_21_0`). `ReportConnectionEnd` and `IsFallbackText` decide which messages get a `-v` line. The old text is pinned by `GopherProtocolHandlerTests.cs` (around line 333) and `GopherProtocolHandlerDiagnosticLogTests.cs` (around line 50).
- curl 8.21.0, `lib/gopher.c` `gopher_do` lines 150-160 at `curl-8_21_0`: a failed `Curl_xfer_send` of the selector or of its CRLF is followed by `failf(data, "Failed sending Gopher request")`. The socket filter has already called `failf` with `Send failure: <error>` for a socket error, and the first `failf` is the message curl prints, so `-v` shows both lines.
- The texts and the reset test (an `IOException` wrapping `SocketException` `ConnectionReset`) are the ones the sibling handlers already use: copy the pattern of `Curl.Protocol.Dict.UnitLibrary/DictIoFailures.cs` (BL-1125, whose Notes give the `-v` order: the message unless it is the fallback text, then `Failed sending DICT request`, then `closing connection #N`); do not reference that library.

## Acceptance criteria

- [ ] `GopherTransferMessages` holds no `Failure when sending data to the peer`.
- [ ] Tests in `Curl.Protocol.Gopher.UnitTests` with a fake connection whose write throws pin, for the selector and for its CRLF: a reset gives exit 55 `Send failure: Connection was reset` and the `-v` lines `Send failure: Connection was reset`, `Failed sending Gopher request`, `closing connection #N`; any other `IOException` gives exit 55 `Failed sending data to the peer` and the `-v` lines `Failed sending Gopher request`, `closing connection #N`.
- [ ] The diagnostic-log test and the other tests that pinned the old text are updated; every other gopher test passes unchanged.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Gopher.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-02: Created.
