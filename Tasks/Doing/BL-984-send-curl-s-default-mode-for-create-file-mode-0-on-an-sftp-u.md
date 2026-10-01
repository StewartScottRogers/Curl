---
id: BL-984
title: Send curl's default mode for --create-file-mode 0 on an SFTP upload, as measured
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-984 — Send curl's default mode for --create-file-mode 0 on an SFTP upload, as measured

## Goal

`-T file --create-file-mode 0 sftp://host/path` sends the `OPEN` permissions curl 8.21.0 sends, as measured.

## Context

- Found in BL-577 (ADR-0258): curl's tool sets `CURLOPT_NEW_FILE_PERMS` only for a non-zero `--create-file-mode`, so `--create-file-mode 0` on an `scp://` upload sends `C0644`. `ScpFileUpload` now does the same, but `SftpFileUpload` still passes the 0 it gets from `ITransferContext.CreateFileMode` into `SSH_FXP_OPEN`.
- Measure with the reference curl against BL-569's OpenSSH 10.2 in WSL (`sftp-server -l DEBUG3` logs the `OPEN` attributes) through `Record-CurlExchange.ps1 -NoServer`: `--create-file-mode 0` and, for comparison, `0600`.

## Acceptance criteria

- [ ] Measured first; the `OPEN` permissions curl sent for `--create-file-mode 0` and the resulting file mode copied into Notes.
- [ ] A test in `Curl.Protocol.Ssh.UnitTests` pins the `OPEN` request's permissions for `--create-file-mode 0` as measured.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
