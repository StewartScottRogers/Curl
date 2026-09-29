---
id: BL-931
title: Write curl's -v info lines and --trace data dumps for the FTP data connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-930]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-931 — Write curl's -v info lines and --trace data dumps for the FTP data connection

## Goal

For `ftp://` and `ftps://`, `-v` shows curl 8.21.0's `* ` info lines about the data connection and the transfer (how the data stream is connected, where it connected, what was sent or received, why the connection is kept or closed), and `--trace`/`--trace-ascii` dump the downloaded and uploaded bytes as curl's `<= Recv data` and `=> Send data` blocks.

## Context

- Audit 2026-09-29, part B: after BL-930 the control-connection lines match, but `FtpSession.cs` reports only a handful of info lines (MDTM, `-z` time condition, `-P` resolution failures) and nothing calls `ReportDataReceived`/`ReportDataSent` for the data connection, so `--trace` of an FTP download has no data blocks.
- Where: `FtpSessionConnections.cs` (passive and active data connections), `FtpSession.cs` (transfer start and end, `QUIT`), `FtpTransferMessages.cs` (keep every measured text there, as the existing messages are).
- Already filed, not here: BL-797 (the passive data connect's via message and `--connect-timeout`), BL-904 (the control host/via data host text on a failed data connect), BL-806 (STARTTLS TLS lines), BL-773 (per-transfer `-v` under `-Z`).
- Measure first with `Record-CurlExchange.ps1 -Ftp -FtpData <text>`: `-v`, `--trace-ascii -` and `--trace -` for a passive `RETR`, an active `-P -` `RETR`, a `-T` `STOR` upload, a `-l` listing, and `-v` for a `--ftp-method nocwd` and a `singlecwd` path. Copy every `* ` line and the data block headers into Notes, with curl's version and build, before pinning text. Pin only what was measured; where curl's line depends on a port or address, pin the format with the loopback values the test uses.

## Acceptance criteria

- [ ] Measured output for the six cases is copied into Notes.
- [ ] `Curl.Protocol.Ftp.UnitTests` pin, through a recording `ITransferEvents`, every measured `* ` info line in order relative to the command and reply lines, and the `ReportDataReceived`/`ReportDataSent` bytes for the download, upload and listing.
- [ ] Existing FTP tests, including BL-930's, pass unmodified.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
