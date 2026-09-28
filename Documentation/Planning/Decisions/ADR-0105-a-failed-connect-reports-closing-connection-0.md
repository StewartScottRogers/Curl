# ADR-0105 — A failed connect reports `closing connection #0`

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-453.

## Context

ADR-0100 has `TcpConnector` report `Trying`, `connect to ... failed: <reason>` and the
exit 7 message, and leaves the connection's end line to the protocol handler. A failed
connect gives the handler no connection, and `ConnectResult.Failed` carries no connection
number (`ConnectResult.ConnectionNumber` is `0` for every failure).

Measured 2026-09-27 with `curl -v -s`:

| Case | curl 8.21.0 (Windows, Schannel) | curl 8.18.0 (Linux, OpenSSL) |
| --- | --- | --- |
| `http://127.0.0.1:1/` (refused, exit 7) | `* closing connection #0` last | `* closing connection #0` last |
| `http://nonexistent.invalid/` (resolve, exit 6) | `* closing connection #0` last | `* shutting down connection #0` last |
| `--connect-timeout 1 http://10.255.255.1/` (exit 28) | `* closing connection #0` last | `* closing connection #0` last |

curl numbers every connection it creates, a failed one included, so a failed connect
after earlier connections in the same run would carry a later number.

## Decision

- **`HttpProtocolHandler` reports `closing connection #N` for every failed connect**, after
  whatever the connector reported, whatever the exit code, as curl 8.21.0 does.
- **`N` is `ConnectResult.ConnectionNumber`**, which is `0` for a failed connect today.
  Numbering failed connects would change `Curl.Protocol.Abstractions.UnitLibrary` and
  `Curl.Networking.UnitLibrary`, outside BL-453; the common case - a single transfer whose
  only connect fails - is `#0` in curl too.

## Consequences

- `curl -v` on a refused, unresolvable or timed-out `http://` URL ends with
  `* closing connection #0`, byte for byte curl 8.21.0.
- A failed connect after an earlier connection in the same run (a redirect to a dead port,
  a reconnect after a pooled connection died) still says `#0` where curl says the next
  number. The fix is a `connectionNumber` on `ConnectResult.Failed` that the connectors set.
- The Linux 8.18.0 build's `shutting down` after a failed resolve is not matched; its
  resolve lines (`Store negative name resolve ...`) are not reported either. Revisit when
  the OpenSSL build measured is 8.21.0.
