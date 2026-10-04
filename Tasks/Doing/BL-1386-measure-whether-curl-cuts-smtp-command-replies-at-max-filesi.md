---
id: BL-1386
title: Measure whether curl cuts SMTP command replies at --max-filesize and match it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: FR-084
created: 2026-10-03
completed:
---
# BL-1386 — Measure whether curl cuts SMTP command replies at --max-filesize and match it

## Goal

An `smtp://` session with no message (`VRFY`, `-X`, `HELP`) and `--max-filesize` writes and exits exactly as curl 8.21.0 does.

## Context

- `Curl.Protocol.Smtp.UnitLibrary/SmtpCommandTransfer.cs` writes every reply line to the output and never reads `ITransferContext.MaxFileSize` (found by BL-1351). curl 8.21.0 writes these replies as body through its client writer, which may apply `--max-filesize` (exit 63, `Exceeded the maximum allowed file size (N) with N bytes`).
- Measure first with `Record-CurlExchange.ps1 -Smtp`, e.g. `curl --max-filesize 10 smtp://127.0.0.1:<port> --mail-rcpt a@b`, with a reply longer than 10 bytes; extend the recorder if it falls short.
- If curl does not cut, the change is a doc note only: say so in the `MaxFileSize` remark's smtp sentence (that file is `Curl.Protocol.Abstractions.UnitLibrary`, add it to `touches`).

## Acceptance criteria

- [ ] curl 8.21.0's output, stderr and exit code for an SMTP `VRFY` reply longer than `--max-filesize` are recorded under Notes.
- [ ] A test in `Curl.Protocol.Smtp.UnitTests` pins Curl's behaviour to that measurement.
- [ ] The `MaxFileSize` remark in `ITransferContext.cs` says what the smtp handler does.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
