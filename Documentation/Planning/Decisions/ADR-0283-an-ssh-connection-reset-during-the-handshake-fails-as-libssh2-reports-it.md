# ADR-0283 — An SSH connection reset during the handshake fails as libssh2 reports it

- **Status:** Accepted
- **Date:** 2026-09-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-991.

## Context

An `sftp://` or `scp://` server that resets the TCP connection (RST, not FIN) made Curl end
with an unhandled `System.IO.IOException` and its stack trace (BL-575): the handshake mapped
only `EndOfStreamException` and `InvalidDataException`, while `NetworkStream` reports a reset
as a plain `IOException`.

Measured 2026-09-30 on the Windows reference build (curl 8.21.0, libssh2 1.11.1) with
`Record-CurlExchange.ps1`, `-sS -k -u user:pw sftp://127.0.0.1:<port>/file`:

| Server | stderr | Exit |
| --- | --- | --- |
| `-Reset`: resets on accept (sftp and scp) | `curl: (2) Failure establishing ssh session: -43, Failed getting banner` | 2 |
| script `reset` (nothing read) | same, `-43` | 2 |
| script `send SSH-2.0-Open`, `reset` (part of a banner, nothing read) | same, `-43` | 2 |
| script `read 5`, `reset` (reads part of curl's banner first) | `... -13, Failed getting banner` | 2 |
| script `read`, `reset` | `... -13, Failed getting banner` | 2 |
| script `send SSH-2.0-OpenSSH_9.6` without CR LF, `read`, `reset` | `... -13, Failed getting banner` | 2 |
| script `send SSH-2.0-OpenSSH_9.6\r\n`, `read`, `reset` (during KEXINIT) | `... -1, Unable to exchange encryption keys` | 2 |
| the same with `close` in place of `reset` | `... -1, Unable to exchange encryption keys` | 2 |

libssh2 reports a failed `recv` as `LIBSSH2_ERROR_SOCKET_RECV` (-43) and a `recv` of 0 bytes as
`LIBSSH2_ERROR_SOCKET_DISCONNECT` (-13). Which of the two a reset before the banner produces
depends on whether the server had read curl's identification before resetting - timing the
client cannot observe; .NET reports every one of them as the same `IOException`.

## Decision

- A reset (any `IOException`) while curl sends its identification or reads the server's is
  exit 2, `Failure establishing ssh session: -43, Failed getting banner`: libssh2's code for a
  receive error, and what the reference build prints for a server that resets on accept - the
  case BL-575 met, an `sshd` whose pre-auth child fails to start.
- From the server's identification to its `KEXINIT` (the client's `KEXINIT` sent included), a
  reset is the close it replaces: exit 2, `-1, Unable to exchange encryption keys`, as measured.
- During the key exchange proper, a reset is likewise the measured close: exit 2, `-8, Unable
  to exchange encryption keys`.

## Consequences

- The late-reset cases the table shows with `-13` print `-43` in Curl: the exit code and the
  rest of the message match, the libssh2 number differs.
- A reset after the key exchange (the `ssh-userauth` service request, SFTP start-up, a
  transfer) is not covered here; BL-1046 measures and maps it. Password and key
  authentication already treat an `IOException` as a failed attempt.
