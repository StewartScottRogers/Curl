# ADR-0550 — `CURLINFO_OS_ERRNO` is the platform's number for the last failed connect attempt

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2374.

## Context

`TransferInfo.OsErrno` (`Curl.Api.UnitLibrary`) answered 0 after every transfer. libcurl's
`CURLINFO_OS_ERRNO` is `data->state.os_errno`, which curl 8.21.0's `lib/cf-socket.c` sets from
`SOCKERRNO` when a connect attempt fails: `WSAGetLastError()` in the Schannel build on Windows, `errno`
in the OpenSSL builds on Linux and macOS. So a refused connect answers `WSAECONNREFUSED` (10061) on
Windows, `ECONNREFUSED` 111 on Linux and 61 on macOS. The curl tool has no `-w` variable for it (its
only use is `--retry-connrefused`, which Curl already reads as `ConnectResult.IsConnectionRefused`), so
the values come from curl's source and the systems' headers rather than a recorded exchange.

`TcpConnector` already knew the last attempt's `SocketError` and dropped it when building the
`ConnectResult`.

## Decision

- `ConnectResult.LastSocketError` carries the last failed attempt's `SocketError`: `Refused` sets
  `ConnectionRefused`, `Failed` takes it as an optional `lastSocketError`, and `TcpConnector`'s dial
  failure and `NumberedConnectFailure` pass it on. A failure with no socket error (a failed resolve, a
  TLS failure) and a success carry `SocketError.Success`.
- `Curl.Engine`'s `EndPointRecordingConnector` hands a failed connect's value to
  `ConnectionEndPointRecorder`, which keeps the last one per transfer and puts it on
  `TransferReport.OsErrno`; `TransferInfoFromReport` copies it to `TransferInfo.OsErrno`.
- The `SocketError` becomes the platform's number through the runtime's own mapping,
  `new SocketException((int)error).NativeErrorCode`, which is the Winsock code on Windows and the
  native `errno` on Linux and macOS. No hand-written `SocketError` to `errno` table is kept: the
  runtime's table is the one its sockets already use to produce the `SocketError`, so it is the exact
  inverse, and each platform's refused value is pinned in its own OS-conditioned test.

## Consequences

- `CURLINFO_OS_ERRNO` after a failed connect matches the platform's curl; after a successful
  transfer with no failed attempt it stays 0.
- A connect that fails for a reason other than a socket error (resolve, TLS, proxy reply) leaves
  it 0, where libcurl may hold a stale `errno` from an earlier system call; matching that stale
  value is not attempted.
