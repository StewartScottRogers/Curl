# ADR-0274 — A failed SFTP open, REALPATH or quote command closes the channel before DISCONNECT

- **Status:** Accepted
- **Date:** 2026-09-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-973.

## Context

ADR-0220 left the session channel open when a download failed before its copy, for the
handler's `DISCONNECT` alone to end, and ADR-0244, ADR-0241 and ADR-0247 followed it for
uploads, listings and `-Q` commands. That was decided before the teardown was measured.
ADR-0247's "Consequences" then recorded that real curl closes the channel first.

The Windows reference build (curl 8.21.0, libssh2 1.11.1, Schannel) was run on 2026-09-30
with `-sS -k --key <rsa key> --pubkey <pub> -u <user>:` against OpenSSH 10.2 in WSL
(`sshd -ddd`, `sftp-server -l DEBUG3`, the setup ADR-0220 and ADR-0247 describe). For a
failed `REALPATH .`, the subsystem was started in a directory removed beneath it, so
`sftp-server` answers `SSH_FX_NO_SUCH_FILE`.

| Case | Requests after `INIT` | Exit | stderr |
| --- | --- | ---: | --- |
| download of a missing file | `REALPATH .`, `OPEN` | 78 | `Could not open remote file for reading: No such file or directory` |
| listing of a missing directory | `REALPATH .`, `OPENDIR` | 78 | `Could not open directory for reading: No such file or directory` |
| upload into a missing directory | `REALPATH .`, `OPEN` write | 78 | `Upload failed: No such file or directory (2/-31)` |
| `-Q "rm /missing"` on a download, a listing and an upload | `REALPATH .`, `REMOVE` | 21 | `rm "/missing" failed: No such file or directory` |
| `REALPATH .` refused, download and listing | `REALPATH .` | 78 | `Remote file not found` |

In every case `sshd` logged `channel 0: rcvd eof`, `send eof`, `send close`, `rcvd close`,
then `Received disconnect ... 11: Shutdown`: libcurl's disconnect runs libssh2's SFTP
shutdown, which closes the channel, before it disconnects the session.

## Decision

`SftpSession.CloseChannelOnFailureAsync` runs a download, listing or upload from
`REALPATH` on. When it throws `SshTransferException`, the channel is closed as a
successful transfer closes it - `EOF`, wait for the server's `CLOSE`, `CLOSE` - and the
failure is then passed on, so the handler's `DISCONNECT` follows. A connection already
broken is left as it is, as `FinishIgnoringFailureAsync` leaves it. Nothing was open to
close in any measured case, so no handle `CLOSE` is sent.

## Consequences

- `SshProtocolHandlerTests.FailedSftpTeardown.cs` pins each measured case through
  `InMemorySshServer`'s events; the fake's `RefusedPaths` refuses a path with status 2.
- ADR-0247's consequence "BL-973 aligns both" is done.
- `--ai-help` is unchanged: no option was added or changed.
