---
id: BL-1230
title: Write curl's 'Failed sending RTSP request' -v line after an RTSP request send fails
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1230 — Write curl's 'Failed sending RTSP request' -v line after an RTSP request send fails

## Goal

When an `rtsp://` request cannot be sent, the transfer's `-v` output gets curl 8.21.0's `Failed sending RTSP request` line after the send failure's own message and before the connection-end line, with the exit code and message unchanged.

## Context

- Today `Curl.Protocol.Rtsp.UnitLibrary/RtspProtocolHandler.cs` `SendAsync` (around lines 185-196) throws `RtspIoFailures.SendFailed(exception)` (exit 55, `Send failure: Connection was reset` or `Failed sending data to the peer`, BL-591), the `catch (RtspTransferException failure)` around line 175 reports it through `Fail` (one `-v` line, the message), and the connection end is reported after it (`RtspVerboseLines.Dropped`). Nothing reports `Failed sending RTSP request`.
- curl 8.21.0, `lib/rtsp.c` `rtsp_do` lines 553-558 at `curl-8_21_0`: `result = Curl_req_send(data, &req_buffer, httpversion); if(result) { failf(data, "Failed sending RTSP request"); goto out; }`. The first `failf` (the socket filter's `Send failure: ...`) stays the message curl prints; the second is only a `-v` line. Every request a transfer sends goes through this path, so it applies to each RTSP request the handler sends, not only the first.
- `Curl.Protocol.Dict.UnitLibrary` did the same for `Failed sending DICT request` (BL-1125); its Notes give the order. Do not reference that library.

## Acceptance criteria

- [ ] Tests in `Curl.Protocol.Rtsp.UnitTests` (beside `RtspProtocolHandlerVerboseTests`) with a fake connection whose write throws pin, for a reset and for any other `IOException`: the exit 55 code and message as today, and the `-v` lines in the order message, `Failed sending RTSP request`, connection-end line.
- [ ] A refused `-H CSeq` or `-H Session` header, which fails before anything is sent, writes no `Failed sending RTSP request` line; an existing or new test pins it.
- [ ] Every other RTSP test passes unchanged.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
