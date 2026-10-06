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
completed: 2026-10-03
---
# BL-1351 — Say in ITransferContext.MaxFileSize's remark that smb, tftp, scp, sftp and ldap enforce it

## Goal

The remark on `ITransferContext.MaxFileSize` names exactly the handlers that read it, and says no handler ignores it that in fact enforces it.

## Context

- `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs`, the `MaxFileSize` remark (lines 83-99), says the `ldap`, `smb`, `smtp`, `scp`/`sftp` and `tftp` handlers "do not read it yet". That is already false for `smb` (BL-1296: `SmbFileTransfer.cs` cuts a download at the limit) and `tftp` (BL-1305: `TftpDownload.cs`), and will be false for `sftp` (BL-1327), `scp` (BL-1328) and `ldap` (BL-1329), which this task waits for.
- Before writing, grep `MaxFileSize` across `Curl.Protocol.*.UnitLibrary` and list the handlers from what the code does, not from this task's title: if one of the three tasks above was finished differently, the remark follows the code. `smtp` only uploads, so say it has no download to limit rather than that it ignores the setting, if that is what the code shows.
- Comment-only change; the build and tests must still pass.

## Acceptance criteria

- [x] The `MaxFileSize` remark lists every handler that reads `MaxFileSize`, `smb`, `tftp`, `scp`/`sftp` and `ldap` included, and names any that still do not with the reason.
- [x] The remark keeps its HTTP and gopher sentences and its ADR-0044 reference.
- [x] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean.

## Notes

- Grepped `MaxFileSize` across `Curl.Protocol.*.UnitLibrary`: ldap, sftp, smb and tftp were already listed by earlier tasks; scp now reads it too (`ScpFileDownload`, BL-1328), so it joined the list. Done directly rather than through align-and-document: a one-sentence comment change.
- smtp does not only upload: `SmtpCommandTransfer` writes the replies of a session with no message (`VRFY`, `-X`, `HELP`) to the output without the limit. The remark says so; whether curl cuts those replies is unmeasured, filed as BL-1386.
- Build clean (0 warnings); `Curl.Protocol.Abstractions.UnitTests` fast tests 705/705 green.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. MaxFileSize remark lists scp with the other enforcing handlers and says why smtp does not read it
