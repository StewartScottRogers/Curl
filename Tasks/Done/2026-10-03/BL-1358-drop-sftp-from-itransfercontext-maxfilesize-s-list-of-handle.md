---
id: BL-1358
title: Drop sftp from ITransferContext.MaxFileSize's list of handlers that do not read it yet
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1358 — Drop sftp from ITransferContext.MaxFileSize's list of handlers that do not read it yet

## Goal

The remark on `ITransferContext.MaxFileSize` lists `sftp` among the handlers that enforce the limit, not among those that do not read it yet.

## Context

- BL-1327 made `SftpFileDownload` cut a download at `MaxFileSize` with exit 63 and `Exceeded the maximum allowed file size (N) with N bytes`; `scp` still ignores it. The remark in `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs` still says "`scp`/`sftp` ... do not read it yet". BL-1327's `touches` did not include the shared contract, so the line was left.
- Check the other names in that list against their handlers while there (BL-1305 TFTP, BL-1296 SMB may have landed too).

## Acceptance criteria

- [x] The `MaxFileSize` remark names `sftp` among the enforcing handlers and `scp` alone among the SSH handlers that do not read it yet.
- [x] Every handler the remark names is true of the code as it is.
- [x] `dotnet build` is clean.

## Notes

- BL-1353 (af4bdd1d) already rewrote the remark: it names `sftp` among the enforcing handlers and `scp` and `smtp` as not reading it yet. No edit was needed.
- Checked every name against the code: dict, file, ftp, gopher, http, imap, ldap, mqtt, pop3, rtsp, sftp (`SftpFileDownload`), smb (`SmbFileTransfer`), telnet, tftp (`TftpDownload`) and ws all read `MaxFileSize`. `ScpFileDownload` is not given it (`SshProtocolHandler.cs:382` passes it to the sftp download only), and `Curl.Protocol.Smtp.UnitLibrary` has no reference to it.
- `dotnet build`: 0 warnings, 0 errors. Fast tests: no failures.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. MaxFileSize remark verified true: sftp enforces, scp and smtp do not (already fixed by BL-1353)
