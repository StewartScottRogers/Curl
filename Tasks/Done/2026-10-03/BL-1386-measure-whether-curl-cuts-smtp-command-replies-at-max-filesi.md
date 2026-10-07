---
id: BL-1386
title: Measure whether curl cuts SMTP command replies at --max-filesize and match it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Abstractions.UnitLibrary]
requirement: FR-084
created: 2026-10-03
completed: 2026-10-03
---
# BL-1386 — Measure whether curl cuts SMTP command replies at --max-filesize and match it

## Goal

An `smtp://` session with no message (`VRFY`, `-X`, `HELP`) and `--max-filesize` writes and exits exactly as curl 8.21.0 does.

## Context

- `Curl.Protocol.Smtp.UnitLibrary/SmtpCommandTransfer.cs` writes every reply line to the output and never reads `ITransferContext.MaxFileSize` (found by BL-1351). curl 8.21.0 writes these replies as body through its client writer, which may apply `--max-filesize` (exit 63, `Exceeded the maximum allowed file size (N) with N bytes`).
- Measure first with `Record-CurlExchange.ps1 -Smtp`, e.g. `curl --max-filesize 10 smtp://127.0.0.1:<port> --mail-rcpt a@b`, with a reply longer than 10 bytes; extend the recorder if it falls short.
- If curl does not cut, the change is a doc note only: say so in the `MaxFileSize` remark's smtp sentence (that file is `Curl.Protocol.Abstractions.UnitLibrary`, add it to `touches`).

## Acceptance criteria

- [x] curl 8.21.0's output, stderr and exit code for an SMTP `VRFY` reply longer than `--max-filesize` are recorded under Notes.
- [x] A test in `Curl.Protocol.Smtp.UnitTests` pins Curl's behaviour to that measurement.
- [x] The `MaxFileSize` remark in `ITransferContext.cs` says what the smtp handler does.

## Notes

- Measured curl 8.21.0 (Schannel) with `Record-CurlExchange.ps1 -Smtp -SmtpReply 'VRFY=250 a-reply-longer-than-ten-bytes'` (a 35-byte reply line):
  - `--max-filesize 10 --mail-rcpt a@b`: stdout `250 a-repl` (10 bytes), stderr `curl: (63) Exceeded the maximum allowed file size (10) with 10 bytes`, exit 63; sent `EHLO`, `VRFY a@b`, `QUIT`.
  - `--max-filesize 40` and three recipients: stdout the first reply plus `250 a`, exit 63 with `(40) with 40 bytes`; sent `VRFY a@b`, `VRFY c@d`, `QUIT` (no third `VRFY`).
  - `--max-filesize 35` (exactly the reply): exit 0, the whole reply written.
- So curl does cut: `SmtpCommandTransfer` now counts written replies against `MaxFileSize`, cuts the write that passes it, fails with exit 63 and still sends `QUIT`. Pinned in `SmtpProtocolHandlerCommandTests` (`ExecuteAsync_ReplyLongerThanMaxFileSize_*`, `ExecuteAsync_SecondReplyPassesMaxFileSize_*`, `ExecuteAsync_ReplyExactlyMaxFileSize_*`, `ExecuteAsync_ContinuationLinePassesMaxFileSize_*`).
- Added `Curl.Protocol.Abstractions.UnitLibrary` to `touches` for the `ITransferContext.MaxFileSize` remark, as the task foresaw; no task in Doing on `origin/work/dark-factory` named it.
- Coverage: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports 0 failing members.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. smtp:// VRFY, -X and HELP replies are cut at --max-filesize with exit 63 and QUIT still sent, as curl 8.21.0 does
