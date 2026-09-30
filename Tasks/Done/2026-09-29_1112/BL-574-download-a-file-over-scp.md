---
id: BL-574
title: Download a file over SCP
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-567]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-574 — Download a file over SCP

## Goal

`scp://host/path` opens a session channel, runs `scp -f <path>` with `exec` as curl 8.21.0's SSH library does, reads the `C<mode> <size> <name>` header and the file bytes with the protocol's acknowledgements, writes the file, and maps a remote error line to curl's exit code and message.

## Context

- Conformance audit 2026-09-28, row 35. Builds on BL-567 (authenticated transport); the channel code from BL-569 is reused if it has landed, otherwise this task introduces the session channel and BL-569 reuses it (both touch only the SSH library, so they never run together).
- **BCL first.** Anything the BCL lacks is hand-built in its own library (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- The SCP wire protocol is not an RFC; take it from the measurement (the command line curl's library sends appears in `sshd -ddd`'s log) and OpenSSH's `scp` behaviour.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: a file, an empty file, a missing file, a directory, `scp://h/~/file`, and `-w '%{size_download}'`.

## Acceptance criteria

- [x] Measured first as above; stdout bytes, stderr and exit code copied into Notes, with the exec command curl sent.
- [x] `Curl.Protocol.Ssh.UnitTests` pin the exec request, the acknowledgement bytes, and output and outcome for each case against the in-memory peer.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Measurement setup.** BL-569's unpacked OpenSSH 10.2 in WSL, run as an unprivileged
  `sshd` (port 2231, `LD_LIBRARY_PATH` to the unpacked libraries) with a `ForceCommand`
  wrapper that logged `$SSH_ORIGINAL_COMMAND` and the bytes curl wrote, and ran OpenSSH's
  own `scp` or, for edge cases, a scripted one chosen by file name (a throwaway bash
  fixture on the WSL side, not committed). The reference curl (8.21.0, libssh2 1.11.1,
  Schannel) ran through `Record-CurlExchange.ps1 -NoServer` with `-sS -k --key <rsa key>
  --pubkey <pub> -u <user>:`.
- **Exec command and acknowledgements.** Every download ran `scp -pf '<path>'` (for example
  `scp -pf '/home/stewart_rogers/bl574/files/hello.txt'`). curl wrote `\0` after the exec
  succeeded, `\0` after the `T` line and `\0` after the `C` line, and nothing after the
  file's bytes (3 bytes for a file, 1 when the first line is an error). OpenSSH's `scp`
  sent `T1790702112 0 1790702112 0\n`, `C0644 11 hello.txt\n`, the bytes and `\0`.
- **Measured against OpenSSH's `scp`** (stdout bytes / stderr / exit):
  - a file: `hello sftp\n` (11 bytes) / empty / 0; with `-w '%{size_download}'`: `hello sftp\n11`
  - an empty file: 0 bytes / empty / 0; with `-w '%{size_download}'`: `0`
  - a missing file: 0 bytes / `curl: (78) Failed to recv file` / 78 (`scp` sent `\x01scp: <path>: No such file or directory\n`); with `-w`: `0`
  - an unreadable file: 0 bytes / `curl: (78) Failed to recv file` / 78
  - a directory, `.../dir` and `.../dir/`: 0 bytes / `curl: (78) Failed to recv file` / 78 (`not a regular file`)
  - `scp://h/~/bl574home.txt`: `home file\n` / empty / 0, sent as `scp -pf 'bl574home.txt'`; `/%7E/...` the same; `/~/` and `/~` sent as they are
  - `scp://h/.../a%20b.txt`: `space name\n` / empty / 0, sent as `'.../a b.txt'`
  - 3,000,000 bytes to `-o` with `-w '%{size_download}'`: `3000000` / empty / 0
  - `/x/it%27s%21''here`: sent as `scp -pf '/x/it'"'"'s'\!"''"'here'`
  - `MaxSessions 0`: `curl: (79) Channel open failure (connect failed)` / 79
- **Measured against the scripted `scp`**: 31 header and data variants; the full table is in
  ADR-0225 (for example a short file is exit 18 `end of response with 5 bytes missing`
  with the 5 bytes, size `-5` is exit 18 `transfer closed with -5 bytes remaining to
  read`, size `-1` reads to the end, a killed connection is exit 79 `Failed reading SCP
  response` in the header and `Error in the SSH layer` in the data). The parse rules were
  then read from libssh2 1.11.1's `src/scp.c` and `src/channel.c`, and match every case.
- **Decisions (ADR-0225, decided under Stewart's delegation):** follow libssh2's byte-level
  checks and messages exactly; close the channel after the copy with the existing
  `CloseAsync`, but leave it open on a header failure as the SFTP open failure does (the
  handler, BL-576, ends the session); an unmeasured break before the exec answer is exit
  79 `Error in the SSH layer`; the exec refusal text is libssh2's source text, since
  OpenSSH cannot be made to refuse an `exec`.
- **Scope.** `SshSessionChannel` gained `RequestExecAsync` and `OpenFailureReasonCode`;
  the connection-failure predicate moved out of `SftpFileDownload` into
  `SshConnectionFailure` so both downloads share it. The ADR and its index row
  (`Documentation/Planning/Decisions`) sit outside `touches`, as every SSH task's ADR
  does; no task in `Doing` names that folder.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. scp:// downloads run scp -pf with exec, acknowledge the T and C lines with libssh2's checks and messages, copy the file and map every measured failure to curl's exit code
