# ADR-0317 — `--tcp-fastopen` and `--mptcp` open the sockets the operating system has

- **Status:** Accepted
- **Date:** 2026-10-01
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-647: both are plain flags with `--no-` forms. libcurl 8.21.0's `cf-socket.c` asks for TCP Fast Open
with `connectx(CONNECT_DATA_IDEMPOTENT)` on Darwin, `setsockopt(TCP_FASTOPEN_CONNECT)` on Linux, and
nothing on Windows, where it connects as usual. For `--mptcp` curl opens the socket with protocol
`IPPROTO_MPTCP` (262) in place of TCP.

Measured on 2026-10-01 (BL-647 Notes): curl 8.21.0 (Schannel, Windows) with `--tcp-fastopen` connects and
prints nothing extra; with `--mptcp` the socket does not open and curl fails with exit 7 after
`failed to open socket: The system could not find the environment option that was entered.` and
`connect to  port 0 from  port 0 failed: No error`. curl 8.18.0 (OpenSSL, Linux) on a WSL kernel without
MPTCP fails the same way with the one line `failed to open socket: Protocol not supported`. Neither build
falls back to plain TCP, whatever BL-647's context assumed.

## Decision

- `--tcp-fastopen` sets TCP Fast Open raw before the connect, wherever the system has an option for a
  client socket (`FastOpenSocketOption`): `TCP_FASTOPEN` (15) on Windows, which `ConnectEx` honours, so
  Curl asks for Fast Open there although curl's Windows build does not; `TCP_FASTOPEN_CONNECT` (30) on
  Linux, curl's own route; and `TCP_FASTOPEN` (`0x105`) on macOS. A refused option is skipped, as libcurl
  only logs the failure.
- `--mptcp` opens every TCP socket with protocol 262 (`TcpSocketOptions.SocketProtocol`) on every
  operating system. Where the system refuses it, the connect fails as curl's does: before each address,
  `AddressFamilyRace` asks `ITcpDialer.FailureToOpenSocket`, writes the platform build's lines in place of
  `Trying`, passes over the address and ends with exit 7 `Failed to connect to ... Could not connect to
  server`. No fallback to TCP.

## Consequences

- On Linux with MPTCP enabled the connection is Multipath TCP; on Windows and macOS, which have no
  `IPPROTO_MPTCP` socket, `--mptcp` fails exactly as curl does there.
- macOS's client Fast Open proper is `connectx` with data in the SYN, which .NET's `Socket` cannot
  express; the `TCP_FASTOPEN` option is the step the BCL allows, and a follow-up task builds the
  `connectx` route.
- `TcpDialer.FailureToOpenSocket` opens and closes one probe socket per address only under `--mptcp`.
