---
id: BL-930
title: Write curl's -v and --trace lines for FTP commands and replies on the control connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-930 — Write curl's -v and --trace lines for FTP commands and replies on the control connection

## Goal

For `ftp://` and `ftps://`, `-v` shows every control-connection command as curl's `> ` line and every reply line as curl's `< ` line, and `--trace`/`--trace-ascii` dump them as curl's `=> Send header` and `<= Recv header` blocks, byte for byte as curl 8.21.0 writes them.

## Context

- Audit 2026-09-29 (Stewart's "logging from --none to --verbose" request, part B): the FTP handler reports only a few `ReportInfo` lines (`FtpSession.cs` lines ~578-613, ~838-856). Nothing in `Curl.Protocol.Ftp.UnitLibrary` calls `ITransferEvents.ReportRequestHeader` or `ReportResponseHeader`, so `curl -v ftp://…` prints none of the `> USER …`/`< 220 …` lines real curl prints, and `--trace` has no header blocks. HTTP, IMAP, POP3, SMTP, RTSP and WebSocket already report theirs (see `Pop3ControlChannel.cs`/`SmtpControlChannel.cs` for the pattern: report the exact bytes written and read, CRLF included).
- Where: `FtpControlChannel.cs` (the one place commands are written and replies read): report each command's bytes through `context.Events.ReportRequestHeader` and each reply line's bytes through `ReportResponseHeader`, including multi-line replies line by line as curl does, and including the commands sent after `AUTH TLS` on the encrypted channel.
- curl writes the `PASS` argument in clear in `-v`; do not mask what curl does not mask. Measure it.
- Measure first: `Record-CurlExchange.ps1 -Ftp` (with `-FtpReply` and `-FtpData`) runs real curl against the loopback FTP responder and records its standard error; run it with `-v`, `--trace-ascii -` and `--trace -` for: an anonymous `RETR`, a login with `-u user:pw`, a multi-line `230-` reply, a `550` on `RETR` (exit 78), and `-v --ssl-reqd` against a responder refusing `AUTH` (exit 64). Copy the measured bytes into Notes before pinning any text. The reference is curl 8.21.0 on the platform's build (Schannel on Windows).
- Not in this task: the data-connection info lines and the body dumps (BL-931), STARTTLS TLS lines (BL-806), the diagnostic log (BL-924).

## Acceptance criteria

- [ ] Measured `-v`, `--trace-ascii -` and `--trace -` output for the five cases above is copied into Notes with the curl version and build.
- [ ] `Curl.Protocol.Ftp.UnitTests` pin, through a recording `ITransferEvents`, the header-out bytes of every command and the header-in bytes of every reply line in the five cases, in curl's order.
- [ ] No renderer changes: the existing `VerboseTransferEventWriter` and `TraceTransferEventWriter` in `Curl.Output` already turn these events into `> `/`< ` lines and `Send header`/`Recv header` blocks. If rendering the measured output needs a change there, stop and file it as a separate task rather than widening this one.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
