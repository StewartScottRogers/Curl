---
id: BL-1314
title: Say in ITransferContext.MaxFileSize's remark that the gopher handler enforces it
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary]
requirement: FR-084
created: 2026-10-03
completed: 2026-10-03
---
# BL-1314 — Say in ITransferContext.MaxFileSize's remark that the gopher handler enforces it

## Goal

The remark on `ITransferContext.MaxFileSize` names every handler that enforces `--max-filesize`, gopher included.

## Context

- `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs`, the `MaxFileSize` remark, says "The `file://` and `http`/`https` handlers enforce it; no other handler reads it yet." That is false: FTP enforces it (`FtpSession`), and since BL-1308 the gopher handler does too: a reply is cut at the limit, with exit 63 and `Exceeded the maximum allowed file size (N) with N bytes`.

## Acceptance criteria

- [x] The `MaxFileSize` remark lists the handlers that read it (grep `MaxFileSize` across `Curl.Protocol.*.UnitLibrary`), gopher and FTP included, and drops "no other handler reads it yet" unless that is still true of the rest.
- [x] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean.

## Notes

- Grepped `.MaxFileSize` across `Curl.Protocol.*.UnitLibrary`: dict, file, ftp, gopher, http, imap, mqtt, pop3, rtsp, telnet and ws read it; ldap, smb, smtp, ssh (scp/sftp) and tftp do not. The remark now lists both groups and gives gopher's exit-63 message. Comment-only change; Abstractions build clean with -warnaserror, its 681 fast tests pass.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. ITransferContext.MaxFileSize's remark names every handler that enforces --max-filesize, gopher and FTP included
