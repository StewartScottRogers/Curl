---
id: BL-933
title: Write curl's -v and --trace lines for a TFTP transfer
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-932]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-933 — Write curl's -v and --trace lines for a TFTP transfer

## Goal

A `tftp://` download or `-T` upload writes curl 8.21.0's `-v` lines and its `--trace`/`--trace-ascii` blocks byte for byte, where today the TFTP handler reports no transfer event at all.

## Context

- Audit 2026-09-29, part B: no file in `Curl.Protocol.Tftp.UnitLibrary` calls any `ITransferEvents` member, so `curl -v tftp://…` prints none of curl's TFTP lines and `--trace` has no blocks. The connect lines for dict, gopher, telnet and mqtt were done by an archived task; TFTP was not part of it.
- Where: `TftpProtocolHandler.cs`, `TftpDownload.cs` (and the upload path), `TftpRetrySchedule.cs` (retransmission timing curl reports), `TftpErrorMapping.cs` (the error-packet text curl prints). Take the sink from `ITransferContext.Events`.
- Measure first with BL-932's `Record-CurlExchange.ps1 -Tftp` (a dependency): `-v`, `--trace-ascii -` and `--trace -` for a download, a download with `--tftp-blksize 1024`, a download with `--tftp-no-options`, a `-T` upload, an ERROR 1 reply (exit 68) and a dropped ACK (a retransmission). Copy every line into Notes with curl's version and build before pinning any text; pin only what was measured, and which of curl's lines are info lines, which data blocks.
- Not in this task: the diagnostic log (BL-927).

## Acceptance criteria

- [ ] Measured output for the six cases is copied into Notes.
- [ ] `Curl.Protocol.Tftp.UnitTests` pin, through a recording `ITransferEvents`, every measured `-v` info line and every data block's bytes in curl's order, for the six cases.
- [ ] Existing TFTP tests pass unmodified.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Tftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
