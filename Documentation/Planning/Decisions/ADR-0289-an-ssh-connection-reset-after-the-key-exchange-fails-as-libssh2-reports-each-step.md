# ADR-0289 — An SSH connection reset after the key exchange fails as libssh2 reports each step

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1046.

## Context

ADR-0283 mapped a reset (RST) during the identification exchange and the key exchange. After
it, a reset still escaped as an unhandled `System.IO.IOException`: `NetworkStream` reports a
reset as a plain `IOException`, while the `ssh-userauth` service request, SFTP start-up and
`SshConnectionFailure.Is` caught only `EndOfStreamException`. Catching every `IOException`
there would be wrong: the same SCP and SFTP steps write to the local output, whose
`IOException` is curl's exit 23, not a broken connection.

Measured 2026-10-01 on the Windows reference build (curl 8.21.0, libssh2 1.11.1), `-sS -k
--key <rsa key> --pubkey <pub> -u <user>: <scheme>://127.0.0.1:<port>/<path> -o <file>`,
`HOME` an empty directory, against OpenSSH 10.2 in WSL (BL-569's unpacked `sshd`, `aes128-ctr`
with `hmac-sha2-256-etm@openssh.com`, so packet lengths stay readable). A throwaway relay
between the two counted curl's packets after `NEWKEYS` and, at the chosen one, reset curl's
socket (linger 0) instead of passing it on; "close" runs shut the socket down instead. WSLx27s Ubuntu curl
(8.18.0, OpenSSL) links the same libssh2 1.11.1.

| Reset while curl waits for the answer to | Exit | stderr |
| --- | ---: | --- |
| the `ssh-userauth` service request (sftp and scp) | 2 | `Failure establishing ssh session: -43, Failed to get response to ssh-userauth request` |
| `USERAUTH_REQUEST none` | 79 | `Error in the SSH layer` |
| the signed `publickey` request | 67 | `Authentication failure` |
| SFTP `CHANNEL_OPEN` | 2 | `Failure initializing sftp session: Unable to startup channel` |
| SFTP `subsystem` request | 2 | `Failure initializing sftp session: Unable to request SFTP subsystem` |
| SFTP `INIT` | 2 | `Failure initializing sftp session: Timeout waiting for response from SFTP subsystem` |
| SFTP `REALPATH .`, `STAT`, the first `READ` | 79 | `Error in the SSH layer` |
| SFTP `OPEN` (reset and close alike, twice each) | 0 | nothing; an empty output file |
| SFTP download after 300000 / 1500000 bytes of the 4 MB file | 79 | `Error in the SSH layer`; 270000 / 1470000 bytes written |
| SCP `CHANNEL_OPEN` (reset and close alike) | 79 | `Unexpected error` |
| SCP `exec` request (reset and close alike) | 79 | `Failed waiting for channel success` |
| SCP's first acknowledgement, before the header (reset and close alike) | 79 | `Failed reading SCP response` |
| SCP download after 300000 / 1500000 bytes | 79 | `Error in the SSH layer`; 294912 / 1474560 bytes written |

The SFTP `OPEN` answer is curl's: libssh2's `sftp_open` fails with a socket error, not an
SFTP status, so curl reads the SFTP status as `SSH_FX_OK`, maps it to `CURLE_OK` and ends
the transfer as a success.

## Decision

- A read or write of the connection that throws `IOException` is wrapped, where the
  transport reads (`SshConnectionReader`) and writes (`SshPacketWriter`), in an
  `SshConnectionLostException`, an `IOException` of its own. `SshConnectionFailure.Is`,
  the service request and SFTP start-up treat it as they treat a close; an `IOException`
  from the local output is not wrapped, so it passes on as before and the console reports
  it.
- Each step then fails as the table shows: the service request with `-43`, SFTP start-up
  with the step's message, SFTP requests and downloads with exit 79 and the bytes written.
- A close or reset at the SFTP download's `OPEN` succeeds with nothing written, as
  measured.
- A close or reset at the SCP download's channel open is exit 79 `Unexpected error`, at its
  `exec` request exit 79 `Failed waiting for channel success`, replacing ADR-0225's
  unmeasured `Error in the SSH layer`.

## Consequences

- `Fakes.ResettingConnection` wraps another connection too, turning its close into a reset,
  and `Fakes.InMemorySshServer.ResetsAt` resets the session at a named step, so
  `SshProtocolHandlerTests.ConnectionReset.cs` pins every row above but the authentication
  ones, which `SshUserAuthentication` already treated as a failed attempt.
- Not measured, so left as they were: a reset at an SFTP upload's or listing's `OPEN`, at an
  SCP upload's start, and the `-Q` commands after a download whose `OPEN` met a reset, which
  run against the broken connection and fail silently, as `FinishIgnoringFailureAsync`
  ignores a broken connection.
