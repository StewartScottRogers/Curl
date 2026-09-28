# ADR-0107 — FTP active mode retries a non-local `-P` address in the handler

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-464.

## Context

curl 8.21.0's `lib/ftp.c`, `ftp_port_bind_socket`, binds active mode's listening socket on
the `-P` address. When that address was given by the user (`possibly_non_local`) and
`bind()` fails with `EADDRNOTAVAIL`, curl prints the info line
`bind(port=%hu) on non-local address failed: %s`, replaces the bind address with the control
connection's own (`getsockname` on the control socket) and restarts the port loop once.
`EPRT`/`PORT` still announce the `-P` address. Any other error, or a second failure, is
`failf(data, "bind(port=%hu) failed: %s")` and exit 30.

Measured with `Record-CurlExchange.ps1 -Ftp` on 2026-09-27 (curl 8.21.0, Schannel build):
`curl -v -P 192.0.2.1 ftp://127.0.0.1:47464/f.txt`, `EPRT` and `PORT` answered `500 no`,
prints `* bind(port=0) on non-local address failed: Address not available` before each of
`EPRT |1|192.0.2.1|61200|` and `PORT 192,0,2,1,239,17`, then exits 30 with
`Failed to do PORT`. Each bind (the one for `EPRT` and the fresh one for `PORT`) retries on
its own.

`ListenTarget` (ADR-0102) holds one address, and `ListenResult` carries an exit code and a
message but no error kind.

## Decision

- **The retry lives in `FtpSession`, the FTP handler**, not in a second address on
  `ListenTarget`. It is FTP behaviour: only the handler knows whether the address came from
  the user (`-P -` never retries) and which address the control connection has.
- **The listener tells the case apart by curl's own wording.** `TcpConnectionListener`
  returns a bind that failed with `EADDRNOTAVAIL` as exit 30 with
  `bind(port=N) on non-local address failed: <reason>`, the exact `-v` line curl prints.
  Every other failure keeps its message.
- **The handler, on that message for a `-P` address that is not `-`**, reports it through
  `ITransferEvents.ReportInfo`, listens once more on the control connection's address with
  the same port range, and announces the `-P` address with the bound port. If the second
  listen fails, or the handler does not retry (`-P -`, or the control connection's address
  is unknown), the exit 30 message is the listener's with ` on non-local address failed: `
  reworded to ` failed: `, which is curl's `failf` text.
- The `<reason>` stays `ConnectFailureReason`'s: `Address not available` on Windows (curl's
  Winsock table, measured), the system's `strerror` text elsewhere.

## Consequences

- No change to `Curl.Protocol.Abstractions.UnitLibrary`: the contract carries the
  distinction in the message it already has.
- The signal is a string: another `IConnectionListener` that wants the retry must word the
  failure the same way. The phrase lives in one constant, `FtpTransferMessages.NonLocalBindFailed`.
- When the control connection's address is unknown, curl would fail `getsockname()` with its
  own message; here the transfer ends with `bind(port=N) failed: <reason>` instead. No
  production connection reports an unknown local address over TCP.

## Alternatives considered

- **A fallback address on `ListenTarget`**, bound by the listener when the first fails with
  `EADDRNOTAVAIL`. It changes the shared contract for one FTP case, and the listener would
  have to report the info line, which it has no `ITransferEvents` for.
- **A new `ListenResult` field or exit code for "address not available"**: cleaner than a
  message, but a contract change for the same result, and no curl exit code exists for it.
