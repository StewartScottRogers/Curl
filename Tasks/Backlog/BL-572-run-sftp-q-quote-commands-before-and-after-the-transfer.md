---
id: BL-572
title: Run SFTP -Q quote commands before and after the transfer
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-569]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-572 — Run SFTP -Q quote commands before and after the transfer

## Goal

`-Q`/`--quote` commands on an SFTP URL (`chgrp`, `chmod`, `chown`, `ln`, `mkdir`, `pwd`, `rename`, `rm`, `rmdir`, `symlink`, `atime`, `mtime`, with `-` for after the transfer and `*` to ignore failure) run as curl 8.21.0 runs them, with an unknown or failing command mapped to exit 21 and curl's message.

## Context

- Conformance audit 2026-09-28, row 35. `-Q` is already parsed and carried as `ITransferContext.QuoteCommands` (the FTP handler uses it). The SFTP command list is in `CurlManual.txt` (`--quote`).
- **BCL only.** If something needed cannot be built on the BCL, move the task to `Blocked` for Stewart naming what is missing; never add a package.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: each command once, a quoted argument with a space, `pwd` (what it prints), an unknown command, a failing `rm` with and without `*`, and a `-` post-transfer command.

## Acceptance criteria

- [ ] Measured first as above; stdout, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` pin the SFTP request each command sends and the outcome for each case against the in-memory peer.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
