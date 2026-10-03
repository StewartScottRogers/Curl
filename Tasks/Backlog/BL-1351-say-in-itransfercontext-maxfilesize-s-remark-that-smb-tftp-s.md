---
id: BL-1351
title: Say in ITransferContext.MaxFileSize's remark that smb, tftp, scp, sftp and ldap enforce it
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1335, BL-1327, BL-1328, BL-1329]
touches: [Curl.Protocol.Abstractions.UnitLibrary]
requirement: FR-084
created: 2026-10-03
completed:
---
# BL-1351 — Say in ITransferContext.MaxFileSize's remark that smb, tftp, scp, sftp and ldap enforce it

## Goal

The remark on `ITransferContext.MaxFileSize` names exactly the handlers that read it, and says no handler ignores it that in fact enforces it.

## Context

- `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs`, the `MaxFileSize` remark (lines 83-99), says the `ldap`, `smb`, `smtp`, `scp`/`sftp` and `tftp` handlers "do not read it yet". That is already false for `smb` (BL-1296: `SmbFileTransfer.cs` cuts a download at the limit) and `tftp` (BL-1305: `TftpDownload.cs`), and will be false for `sftp` (BL-1327), `scp` (BL-1328) and `ldap` (BL-1329), which this task waits for.
- Before writing, grep `MaxFileSize` across `Curl.Protocol.*.UnitLibrary` and list the handlers from what the code does, not from this task's title: if one of the three tasks above was finished differently, the remark follows the code. `smtp` only uploads, so say it has no download to limit rather than that it ignores the setting, if that is what the code shows.
- Comment-only change; the build and tests must still pass.

## Acceptance criteria

- [ ] The `MaxFileSize` remark lists every handler that reads `MaxFileSize`, `smb`, `tftp`, `scp`/`sftp` and `ldap` included, and names any that still do not with the reason.
- [ ] The remark keeps its HTTP and gopher sentences and its ADR-0044 reference.
- [ ] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-10-03: Created.
