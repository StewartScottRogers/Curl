---
id: BL-385
title: Stream unseekable 7bit multipart file parts and fail the send on a refused byte
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-301]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-385 — Stream unseekable 7bit multipart file parts and fail the send on a refused byte

## Goal

A `-F 'f=@<pipe>;encoder=7bit'` file part that cannot seek is streamed through `EncodedReadStream` instead of read whole, and a byte above 127 met while sending fails the transfer with exit 26 and `read error getting mime data`, as curl 8.21.0 does.

## Context

- Found while delivering BL-301 (2026-09-27). ADR-0076 streams every encoded file part except an unseekable `7bit` one, which `MultipartFormBodyBuilder.AddEncodedFileAsync` still reads whole, because it cannot be checked while building and read again.
- `EncodedReadStream` already throws `MultipartDataRefusedException` (an `IOException`, message `read error getting mime data`) when a read reaches a refused byte.
- `HttpRequestBodyWriter.ReadAsync` (`Curl.Protocol.Http.UnitLibrary`) takes every failed read as the end of the body: a chunked body is then sent short with no error, and a body of known length fails with `client mime read EOF fail`. It needs to report a failed read of a multipart body as exit 26 with curl's message; measure what curl 8.21.0 prints (`lib/mime.c`, `encoder_7bit_read` returns `STOP_FILLING`/`READ_ERROR`) before pinning it.

## Acceptance criteria

- [x] A test in `Curl.Core.UnitTests` shows an unseekable `7bit` file part is not read while building.
- [x] A test in `Curl.Protocol.Http.UnitTests` shows a chunked multipart body whose read throws `IOException` mid-body fails with exit 26 and the message measured from curl 8.21.0.
- [x] `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for both libraries.

## Notes

- Measured with `Record-CurlExchange.ps1`: curl 8.21.0 (Windows), `-F 'f=@f.bin;encoder=7bit'`, 200000 bytes with 0xC8 at 150000: exit 26, `curl: (26) read error getting mime data`, 65536 request bytes sent. Windows curl gives a named pipe (`@\\.\pipe\x`) the size 0 and sends a 240-byte `Content-Length` body, so the chunked case was measured with WSL's curl 8.18.0 (Linux) from a FIFO: `Transfer-Encoding: chunked`, the chunks before the byte, no closing `0` chunk, exit 26, `read error getting mime data`.
- A plain `IOException` from a form file must stay curl's end of data: `HttpProtocolHandlerTests.ExecuteAsync_StreamBodyReadFails_FailsWithExit26AndTheMeasuredMessage` pins a locked file as `client mime read EOF fail`. So the sender needs to tell the encoder's refusal apart. Decision (ADR-0093, decided by Claude under Stewart's delegation): new `RequestBodyReadFailedException : IOException` in `Curl.Protocol.Abstractions`; `EncodedReadStream` throws it (replacing the internal `MultipartDataRefusedException`); `HttpRequestBodyWriter.ReadAsync` turns it into `HttpTransferException` exit 26 with its message. The Http criterion's "read throws `IOException`" is met by this `IOException` subtype.
- Added `Curl.Protocol.Abstractions.UnitLibrary`, `Curl.Protocol.Abstractions.UnitTests` and `Documentation/Planning/Decisions` to `touches` for the contract type and ADR-0093; no task in `Doing` (BL-334: Curl.Console; BL-430: one Curl.Cli.UnitTests file) names them.
- Core: an unseekable `7bit` file is streamed through `EncodedReadStream` like base64/qp; `AddEncodedFileAsync` is gone and `AddEncodedData` lost its always-true `dataLengthIsKnown`. Tests: `ASevenBitFileThatCannotSeekIsNotReadWhileBuildingAndLeavesTheLengthUnknown`, `ASevenBitFileThatCannotSeekFailsTheReadThatReachesARefusedByte`, `ASevenBitFileThatCannotSeekOrBeReadFailsTheBodysRead`. Http: `WriteAsync_ChunkedMultipartBodyReadIsRefusedMidBody_FailsWithExit26AndTheMeasuredMessage`, `WriteAsync_MultipartBodyOfKnownLengthReadIsRefused_FailsWithExit26AndTheMeasuredMessage`.
- Measure-CodeQuality: Core, Http and Abstractions all 100% line, 100% branch, 0 failing members.
- `dotnet format --verify-no-changes` still reports end-of-line markers in files outside this task (Curl.Output, Curl.Cli.UnitTests, Curl.Console.UnitTests); left alone.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. An unseekable 7bit multipart file part streams, and a refused byte fails the send with exit 26 'read error getting mime data'
