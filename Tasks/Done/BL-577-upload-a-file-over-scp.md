---
id: BL-577
title: Upload a file over SCP
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-574]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-577 — Upload a file over SCP

## Goal

`-T file scp://host/path` runs `scp -t <path>`, sends the `C<mode> <size> <name>` header with curl 8.21.0's mode (from `--create-file-mode`, default as measured), the bytes and the terminating zero byte with acknowledgements, and maps a remote error to curl's exit code and message; `-T -` behaves as curl's does for an unknown size.

## Context

- Conformance audit 2026-09-28, row 35. Builds on BL-574's exec channel.
- **BCL first.** Anything the BCL lacks is hand-built in its own library (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: a new file, an existing file, a missing directory, a read-only target, `--create-file-mode 0600`, and `-T -`.

## Acceptance criteria

- [x] Measured first as above; stderr, exit code and the resulting remote file (size and mode) copied into Notes, with the exec command curl sent.
- [x] `Curl.Protocol.Ssh.UnitTests` pin the header, data and acknowledgements and the outcome for each case against the in-memory peer.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Measurement setup.** BL-569's OpenSSH 10.2 in WSL as an unprivileged `sshd` on port
  2231 (`~/bl577/sshd_config`, `LogLevel DEBUG3`) with a `ForceCommand` wrapper that logged
  `$SSH_ORIGINAL_COMMAND`, curl's bytes and `scp`'s answers, and ran OpenSSH's own `scp` or,
  by file name, a scripted one; a second `sshd` on 2232 with `MaxSessions 0`. The reference
  curl (8.21.0, libssh2 1.11.1, Schannel) ran through `Record-CurlExchange.ps1 -NoServer`
  with `-sS -k --key <rsa key> --pubkey <pub> -u <user>: -w '%{size_upload}' -T <source>`.
- **Exec command and bytes.** `scp -t '/home/stewart_rogers/bl577/files/new.txt'`. `scp`
  answered `\0` at once; curl sent `C0644 10 new.txt\n`; `scp` answered `\0`; curl sent the
  10 bytes and then `EOF` - **no terminating zero byte**, contrary to the Goal's wording
  (OpenSSH's `scp` exits 1 on the missing byte but has already written the file), and no `T`
  line. After a success curl sends `EOF`, waits for the server's `CLOSE`, sends its own;
  after a refusal it sends `EOF` and `CLOSE` together, then waits.
- **Measured against OpenSSH's `scp`** (stderr / exit / remote file):
  - new file: empty / 0 / 10 bytes `-rw-r--r--`; `size_upload` 10
  - existing file: empty / 0 / replaced, 10 bytes, mode kept `-rw-r--r--`
  - missing directory (`nodir/x.txt`): `curl: (25) failed to send file` / 25 / none (`scp`: `\x01scp: …: No such file or directory`); `size_upload` 0
  - read-only target (`-r--r--r--`): `curl: (25) failed to send file` / 25 / unchanged, 3 bytes (`Permission denied`)
  - read-only directory, and a directory as the target: `curl: (25) failed to send file` / 25
  - `--create-file-mode 0600`: `C0600 10 m600.txt` / 0 / `-rw-------`; `0777` and `777`: `C0777`, `-rwxr-xr-x` (umask); `0`: `C0644`; `1`: `C01`, `scp` refuses `bad mode`, exit 25 `failed to send file`; `1000`, `4755`, `7777`: exit 2 `option --create-file-mode: too large number` (the parser's, already done)
  - `-T -`: `curl: (25) SCP requires a known file size for upload` / 25, after authentication and before any channel (also on the `MaxSessions 0` server)
  - empty file: `C0644 0 empty.txt` / 0 / 0 bytes; 100000 and 5000000 bytes: 0, every byte
  - `/~/bl577/files/tilde.txt`: `scp -t 'bl577/files/tilde.txt'`, name `tilde.txt`; `it%27s%21x.txt`: `scp -t '…/it'"'"'s'\!'x.txt'`, name `it's!x.txt`
  - `MaxSessions 0`: `curl: (25) Channel open failure (connect failed)` / 25
- **Measured against the scripted `scp`**: first answer `\x01…`, `\x02…` or `X` is 25
  `Invalid ACK response from remote`; the channel ending is 25 `Unexpected channel close`;
  a killed connection 25 `SCP failure`. Answer to the `C` line `\x02…`, bare `\x01` or `X…` is
  25 `failed to send file`; ending 25 `Unexpected channel close`; killed 25 `Invalid ACK
  response from remote`. An error line after the bytes, or 3 s of silence, is still 0. A
  connection killed during a 5 MB upload is 79 `Error in the SSH layer`, `size_upload`
  1703800.
- **Decisions (ADR-0258, decided under Stewart's delegation):** follow the measurements and
  libssh2's `scp_send`; `--create-file-mode 0` sends 0644 because curl leaves the option
  unset for 0; the two breaks OpenSSH cannot produce (before the channel is confirmed or
  the `exec` answered) are `SCP failure`, the nearest measured point; the `exec` refusal
  keeps ADR-0225's text at exit 25. ADR number 0258 skips 0254-0257, which other lanes'
  worktrees already hold.
- **Scope.** `SshSessionChannel` gained `CloseAtOnceAsync`; `SshTransferException` gained
  the upload failures (exit 25); `ScpCommand.ForUpload`; the handler sends an `scp` upload
  to `ScpFileUpload`. The fakes gained `scp -t` in `InMemorySshServerSession`,
  `ScpServerScript.Receiving` and a shared `UnseekableStream`. The ADR and its index row sit
  outside `touches`, as every SSH task's ADR does; no task in `Doing` names that folder.
- **Follow-up:** BL-982 measures `--create-file-mode 0` on an SFTP upload, which still
  passes 0 through.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. scp:// uploads run scp -t, send the C line with --create-file-mode and the bytes between scp's acknowledgements, refuse an unknown size and map every measured failure to curl's exit code and message
