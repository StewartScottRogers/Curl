# ADR-0100 — The TCP connector reports its dials and the connection it opens

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

ADR-0046 gave `ConnectTarget` an `Events` sink so the connector can report what `-v`
prints about connecting. BL-408 makes `TcpConnector` report it. Measured on curl 8.21.0
(mingw, Schannel), 2026-09-27:

- a success prints `*   Trying 127.0.0.1:18441...` then
  `* Established connection to 127.0.0.1 (127.0.0.1 port 18441) from 127.0.0.1 port 55116 `;
  on HTTPS the `Established connection` line comes after the ALPN lines (BL-228);
- `curl -v -s http://127.0.0.1:1/` prints `*   Trying 127.0.0.1:1...`,
  `* connect to 127.0.0.1 port 1 from 0.0.0.0 port 56585 failed: Connection refused`,
  `* Failed to connect to 127.0.0.1:1 after 2025 ms: Could not connect to server` and
  `* closing connection #0`.

## Decision

1. **`Trying <end point>...` before every dial, the address in resolver order**, IPv6
   bracketed as `IPEndPoint` writes it (`[::1]:80`). *Why:* curl prints one per address
   it tries.
2. **`ReportConnectionOpened` once the connection is ready for the transfer**: after the
   TCP connect, any tunnel, and the target's TLS handshake; never when any of them fails.
   It names the host dialled (the `--connect-to` destination, or the proxy) and the address
   dialled. *Why:* BL-228 measured the line after the ALPN lines; the other orders are
   unmeasured and this is the one place every path meets.
3. **`TcpConnector` numbers the connections it opens from `0`** and returns the number in
   `ConnectResult.ConnectionNumber`. *Why:* the event needs one and the connector is the
   first to know. `PoolingConnector` keeps numbering the successes it is handed from `0`,
   so the two agree in order; the `Established` line does not print the number.
4. **A failed dial reports `connect to <address> port <port> from 0.0.0.0 port 0 failed:
   <reason>`** (`::` for IPv6). *Why:* `ITcpDialer` reports a failure as a
   `SocketException`, which carries no local end point; curl's ephemeral port changes on
   every run, so no script can depend on it.
5. **The reason is `ConnectFailureReason`'s**: on Windows curl's own Winsock words
   (`Connection refused` measured, the rest from curl's `lib/strerror.c`), falling back to
   the system's message; elsewhere the system's message, which is `strerror` as the
   OpenSSL builds print it. *Why:* match the platform's curl.
6. **The exit 7 message (direct or over a proxy) is also reported as an info line**, as
   curl's `failf` repeats every error into `-v`. *Why:* the measured output has it. A later
   change that repeats every error message generically must take this one out.
7. **`closing connection #0` is not the connector's.** *Why:* it is the transfer's end,
   which the protocol handler reports for connections it holds; a connect that failed gives
   the handler no connection to number, so that line is left to a follow-up task.

## Consequences

`-v` and the trace dumps now show the connect lines for every TCP protocol without a
handler change. The connector reports on whatever sink the handler put on the target, so
a transfer nobody listens to pays only for the strings.
