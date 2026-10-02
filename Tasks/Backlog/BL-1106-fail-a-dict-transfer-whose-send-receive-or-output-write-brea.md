---
id: BL-1106
title: Fail a dict transfer whose send, receive or output write breaks with curl's exit 55, 56 or 23
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Dict.UnitLibrary, Curl.Protocol.Dict.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1106 — Fail a dict transfer whose send, receive or output write breaks with curl's exit 55, 56 or 23

## Goal

A `dict://` transfer whose request write, reply read or output write throws an `IOException` ends with the `TransferResult` curl 8.21.0 gives it (exit 55, 56 or 23 and curl's message) instead of letting the exception escape `DictProtocolHandler.ExecuteAsync`.

## Context

- Today `Curl.Protocol.Dict.UnitLibrary/DictProtocolHandler.cs` catches nothing: `ExchangeAsync` calls `connection.WriteAsync` and `FlushAsync`, and `CopyReplyAsync` calls `connection.ReadAsync` and `context.Output.WriteAsync`, all unguarded. No test in `Curl.Protocol.Dict.UnitTests` fails a connection or the output.
- curl 8.21.0, `lib/dict.c` `dict_do` (https://github.com/curl/curl/blob/curl-8_21_0/lib/dict.c): a failed `sendf` is `failf(data, "Failed sending DICT request")` and the send's own error. The socket filter (`lib/cf-socket.c` lines 1562 and 1618 at `curl-8_21_0`) has already called `failf` with `Send failure: <error>` / `Recv failure: <error>`, and the first `failf` is the message curl prints, so stderr shows `curl: (55) Send failure: Connection was reset` while `-v` shows both lines. A failed read is exit 56; a refused output write is exit 23 `Failure writing output to destination, passed N returned M`.
- The texts are already measured and used by sibling handlers. Copy the pattern, not the type (a protocol library never references another): `Curl.Protocol.Rtsp.UnitLibrary/RtspIoFailures.cs` (reset versus other `IOException`, and `OutputWriteFailedException.BytesAccepted` for the `returned` count) and `Curl.Protocol.Gopher.UnitLibrary/GopherProtocolHandler.cs` (`TrySendAsync`, `CopyReplyAsync`, `ReportConnectionEnd`: after a failure the `-v` line is the message and then `closing connection #N`, not `shutting down connection #N`).
- A cancelled token must still throw `OperationCanceledException` (existing test `ExecuteAsync_CancelledToken_ThrowsOperationCanceledAndDisposesTheConnection`).

## Acceptance criteria

- [ ] New tests in `Curl.Protocol.Dict.UnitTests` with a fake connection whose write throws: a reset (`IOException` wrapping `SocketException` `ConnectionReset`) returns exit 55 `Send failure: Connection was reset`, any other `IOException` returns exit 55 `Failed sending data to the peer`; `-v` events then report `Failed sending DICT request` and `closing connection #N`.
- [ ] New tests with a fake connection whose read throws return exit 56 (`Recv failure: Connection was reset` for a reset, `Failure when receiving data from the peer` otherwise) with the bytes written before the failure counted.
- [ ] A new test with an output stream that refuses a write returns exit 23 `Failure writing output to destination, passed N returned M`, N being the bytes passed and M those `OutputWriteFailedException.BytesAccepted` reports (0 for a plain `IOException`).
- [ ] The existing Dict tests still pass unchanged; `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Dict.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
