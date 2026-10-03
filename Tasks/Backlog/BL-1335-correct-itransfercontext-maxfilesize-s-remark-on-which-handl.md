---
id: BL-1335
title: Correct ITransferContext.MaxFileSize's remark on which handlers read it
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary]
requirement: FR-084
created: 2026-10-03
completed:
---
# BL-1335 — Correct ITransferContext.MaxFileSize's remark on which handlers read it

## Goal

The remark on `ITransferContext.MaxFileSize` in `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs` names exactly the handlers that enforce the limit today, with `ldap`, `smb` and `tftp` among them.

## Context

- The remark (around line 89) still says "the `ldap`, `smb`, `smtp`, `scp`/`sftp` and `tftp` handlers do not read it yet".
- BL-1329 made the LDAP search enforce it (`LdapEntryWriter`), BL-1305 TFTP and BL-1296 SMB. BL-1327 (SFTP) is in progress; check whether it has reached Done before wording `scp`/`sftp`.
- BL-1329 could not edit the remark: BL-1325 held `Curl.Protocol.Abstractions.UnitLibrary` at the time.

## Acceptance criteria

- [ ] The remark lists `ldap`, `smb` and `tftp` among the handlers that enforce `MaxFileSize`, and no handler that does not read it is listed as enforcing it (check with `grep -rn MaxFileSize Curl.Protocol.*.UnitLibrary`).
- [ ] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-10-03: Created.
