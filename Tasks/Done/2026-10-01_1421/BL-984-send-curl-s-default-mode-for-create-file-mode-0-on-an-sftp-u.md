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
completed: 2026-10-01
---
# BL-984 — Send curl's default mode for --create-file-mode 0 on an SFTP upload, as measured

## Goal

`-T file --create-file-mode 0 sftp://host/path` sends the `OPEN` permissions curl 8.21.0 sends, as measured.

## Context

- Found in BL-577 (ADR-0258): curl's tool sets `CURLOPT_NEW_FILE_PERMS` only for a non-zero `--create-file-mode`, so `--create-file-mode 0` on an `scp://` upload sends `C0644`. `ScpFileUpload` now does the same, but `SftpFileUpload` still passes the 0 it gets from `ITransferContext.CreateFileMode` into `SSH_FXP_OPEN`.
- Measure with the reference curl against BL-569's OpenSSH 10.2 in WSL (`sftp-server -l DEBUG3` logs the `OPEN` attributes) through `Record-CurlExchange.ps1 -NoServer`: `--create-file-mode 0` and, for comparison, `0600`.

## Acceptance criteria

- [x] Measured first; the `OPEN` permissions curl sent for `--create-file-mode 0` and the resulting file mode copied into Notes.
- [x] A test in `Curl.Protocol.Ssh.UnitTests` pins the `OPEN` request's permissions for `--create-file-mode 0` as measured.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Measurement setup.** BL-569's OpenSSH 10.2 in WSL as an unprivileged `sshd` on port 2241
  (`~/bl984/sshd_config`), its `sftp` subsystem a wrapper that `tee`s curl's SFTP bytes to a
  file before OpenSSH's `sftp-server` (with `-l DEBUG3 -e` the log went to the channel's
  stderr, not the sshd log, so the raw packet was read instead). Reference curl 8.21.0
  (libssh2 1.11.1, Schannel) through `Record-CurlExchange.ps1 -NoServer` with
  `-sS -k --key <key> --pubkey <pub> -u <user>: --create-file-mode <m> -T src.txt` (10 bytes).
- **`--create-file-mode 0`:** exit 0, stderr empty; `OPEN` pflags `0x1a` (WRITE|CREAT|TRUNC),
  attrs flags `0x04` (PERMISSIONS), permissions `0x000081a4` (S_IFREG | 0644); the remote
  file is `-rw-r--r--`.
- **`--create-file-mode 0600`:** exit 0; permissions `0x00008180` (S_IFREG | 0600); the remote
  file is `-rw-------`.
- So, as on `scp://` (ADR-0258), curl leaves `CURLOPT_NEW_FILE_PERMS` unset for 0 and libssh2
  opens with curl's default 0644. `SftpFileUpload` now does the same, and the
  `--ftp-create-dirs` reopen uses the same mode. No ADR: the behaviour is measured, not chosen.
- Pinned by `SftpFileUploadTests.UploadAsync_CreateFileModeZero_SendsCurlsDefault0644AsTheOpensPermissionsAsMeasured`.
  Build clean; fast tests green across the solution (Ssh: 1498 passed);
  `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary`: line 100, branch 100,
  842 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. --create-file-mode 0 on an SFTP upload opens with curl's default 0644, as measured
