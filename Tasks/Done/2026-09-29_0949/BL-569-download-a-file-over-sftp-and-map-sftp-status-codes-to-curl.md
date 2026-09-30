---
id: BL-569
title: Download a file over SFTP and map SFTP status codes to curl's exit codes
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-567]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-569 — Download a file over SFTP and map SFTP status codes to curl's exit codes

## Goal

`sftp://host/path` opens a session channel (RFC 4254), starts the `sftp` subsystem, exchanges `SSH_FXP_INIT`/`VERSION` (version 3), resolves `~/` paths as curl 8.21.0 does, and downloads the file with `OPEN`/`READ`/`CLOSE`, writing its bytes and reporting progress, with each `SSH_FX_*` status (no such file, permission denied, failure) mapped to curl's exit code and message (78, 9 and so on as measured).

## Context

- Conformance audit 2026-09-28, row 35. Builds on BL-567 (authenticated transport). Channel windowing and `SSH_MSG_CHANNEL_WINDOW_ADJUST` belong here.
- **BCL only.** No new primitive needed. Anything the BCL lacks is hand-built in its own library (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- SFTP v3: draft-ietf-secsh-filexfer-02.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: a file, an empty file, a missing file, a file without read permission, `sftp://h/~/file`, a path with `%20`, and `-w '%{size_download}'`.

## Acceptance criteria

- [x] Measured first as above; stdout bytes, stderr and exit code of each copied into Notes.
- [x] `Curl.Protocol.Ssh.UnitTests` pin the SFTP requests and the output and outcome for each case against the in-memory peer, including a file larger than one channel window.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Measurement setup.** No `sshd` ships on the lane machine, so Ubuntu's OpenSSH 10.2
  packages (`openssh-server`, `openssh-sftp-server`, `libwrap0`) were unpacked in WSL and
  run as an unprivileged `sshd -D` with public-key authentication. The reference curl
  (8.21.0, libssh2 1.11.1, Schannel) ran as `curl -sS -k --key <rsa key> --pubkey <pub>
  -u <user>: sftp://localhost:<port>/<path>`, once against OpenSSH's `sftp-server` and
  once against a scripted SFTP subsystem (a throwaway C# app, not committed) that logged
  every request and answered by path. `Record-CurlExchange.ps1 -NoServer` was not needed:
  the server was OpenSSH itself.
- **Measured against `sftp-server`** (stdout bytes / stderr / exit):
  - a file: `hello sftp\n` (11 bytes) / empty / 0
  - an empty file: 0 bytes / empty / 0; with `-w '%{size_download}'`: `0` / empty / 0
  - a missing file: 0 bytes / `curl: (78) Could not open remote file for reading: No such file or directory` / 78; with `-w '%{size_download}'`: `0`
  - a file without read permission: 0 bytes / `curl: (9) Could not open remote file for reading: Permission denied` / 9
  - `sftp://h/~/bl569home.txt`: `home file\n` / empty / 0
  - `sftp://h/.../a%20b.txt`: `space name\n` / empty / 0
  - 3,000,000 random bytes to `-o` with `-w '%{size_download}'`: `3000000` / empty / 0
  - the 11-byte file to stdout with `-w '%{size_download}'`: `hello sftp\n11` / empty / 0
  - a directory named without a trailing slash: 0 bytes / `curl: (79) Error in the SSH layer` / 79
- **Measured against the scripted subsystem**: the request sequence (`INIT` 3, `REALPATH .`,
  `OPEN` with flags `READ` and attrs `PERMISSIONS 0100644`, `STAT`, `READ`s, `CLOSE`), the
  read-ahead pattern, every `OPEN` status 0-22 and 99, `READ` statuses, short files,
  `REALPATH` failures, `VERSION` variants and a refused subsystem. The full table is in
  ADR-0220. OpenSSH's `DEBUG3` log gave the channel's window (2097152), packet size
  (32768), five window adjustments of ~540 KB over 3 MB, and the teardown order.
- **Decisions (ADR-0220, decided under Stewart's delegation):** grant the whole window
  back once under three quarters remain (libssh2's buffer term is unobservable); a channel
  that ends before `VERSION` fails at once with the lost-connection message rather than
  hanging as curl does; an empty `DATA` answer counts as end of file; a connection that
  breaks between session start and the copy is exit 79. The download leaves the channel
  open: shutdown (`SftpSession.ShutdownAsync`) and `DISCONNECT` belong to the handler,
  BL-576.
- **Scope.** The ADR (`Documentation/Planning/Decisions`) sits outside `touches`, as every
  SSH task's ADR does; no task in `Doing` names `Documentation`. `SshWireReader` gained
  `RemainingLength` for the `VERSION` extension check.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SFTP download over a session channel with curl's requests, read-ahead and status-to-exit-code mapping, pinned against an in-memory peer; ADR-0220
