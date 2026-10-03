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
completed:
---
# BL-1358 — Drop sftp from ITransferContext.MaxFileSize's list of handlers that do not read it yet

## Goal

The remark on `ITransferContext.MaxFileSize` lists `sftp` among the handlers that enforce the limit, not among those that do not read it yet.

## Context

- BL-1327 made `SftpFileDownload` cut a download at `MaxFileSize` with exit 63 and `Exceeded the maximum allowed file size (N) with N bytes`; `scp` still ignores it. The remark in `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs` still says "`scp`/`sftp` ... do not read it yet". BL-1327's `touches` did not include the shared contract, so the line was left.
- Check the other names in that list against their handlers while there (BL-1305 TFTP, BL-1296 SMB may have landed too).

## Acceptance criteria

- [ ] The `MaxFileSize` remark names `sftp` among the enforcing handlers and `scp` alone among the SSH handlers that do not read it yet.
- [ ] Every handler the remark names is true of the code as it is.
- [ ] `dotnet build` is clean.

## Notes

## Log

- 2026-10-03: Created.
