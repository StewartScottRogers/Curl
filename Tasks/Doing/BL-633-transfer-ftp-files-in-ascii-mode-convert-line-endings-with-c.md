---
id: BL-633
title: Transfer FTP files in ASCII mode, convert line endings with --crlf and append with -a
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-632, BL-913]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-633 — Transfer FTP files in ASCII mode, convert line endings with --crlf and append with -a

## Goal

`-B` (and a `;type=a` URL suffix) makes the FTP handler send `TYPE A` and treat the data as curl 8.21.0 does; `--crlf` converts LF to CRLF on FTP uploads (and `file://` uploads, FR-013); `-a` uploads with `APPE` instead of `STOR`; each measured byte for byte.

## Context

- Conformance audit 2026-09-28, row 24 (Major). Options: BL-632.
- Code: `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs` and the handler (`TYPE I` today; `APPE` is already used for some resume paths, `FtpUploadOffset.cs`); context mapping in `Curl.Console/TransferContextFactory.cs` (`ConvertLineEndings` exists; if ASCII mode or append needs a new `ITransferContext` member, file an Abstractions task for it and depend on it rather than widening this one).
- `Record-CurlExchange.ps1 -Ftp` records the commands and the upload bytes (`upload.bin`).

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Ftp`: `-B` download, `ftp://h/f;type=a`, `-T f --crlf` with LF and CRLF lines, `-T f -a`, and `-T f -a -C -`; commands and `upload.bin` copied into Notes.
- [ ] `Curl.Protocol.Ftp.UnitTests` pin the commands and data bytes for each case; a `Curl.Console.UnitTests` test shows `--crlf` reaching a `file://` upload.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-09-29 (lane 3): `ITransferContext` has `ConvertLineEndings` but no member for `-B` or `-a`, so neither can reach the FTP handler (`TransferContext` is its only implementer). As this task's Context directs, the contract change is filed as BL-913 (touches `Curl.Protocol.Abstractions.UnitLibrary`/`.UnitTests`) and this task depends on it. No code was changed here.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Waits on BL-913: ITransferContext needs UseAscii and Append before -B and -a can reach the FTP handler
- 2026-09-29: Backlog -> Doing.
