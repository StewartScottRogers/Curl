# ADR-0292 — QUIC's UDP socket binds as `--interface` and `--local-port` ask, and a failed bind ends as the TCP path's

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1025.
Extends ADR-0269 (BL-600) from TCP dials to QUIC's UDP sockets.

## Context

ADR-0269 bound every TCP dial through `LocalBindingTcpDialer`, but `TcpConnector.ConnectMultiplexedAsync`
handed addresses to `QuicDialer`, whose `IUdpChannelOpener` bound any address on an ephemeral port.
libcurl's `bindlocal` binds QUIC's socket as it binds a TCP one.

Measured on 2026-10-01 with curl.se's Windows build, curl 8.18.0 (LibreSSL, ngtcp2), the reference QUIC
build (ADR-0144, ADR-0180), through `Record-CurlExchange.ps1 -UdpSink -Port 47611`
(full table in BL-1025 Notes):

- `--http3-only -v --interface 127.0.0.1 --local-port 41000-41010` binds: `Local port: 41000`, then the
  trust anchors and the handshake (exit 28 against the silent sink).
- `--local-port 41000-41005` with 41000-41002 taken walks the range: `Local port: 41003`.
- `--interface bogus0`, `host!nosuch.invalid`, and `--local-port 41000-41002` all taken: exit 45, with the
  `-v` line `Failed to connect to 127.0.0.1 port 47611 after <n> ms: Failed binding local connection end`,
  no `QUIC connect to` line and no trust-anchor lines, so the socket is bound before them.
- `--interface ::1` to `127.0.0.1`: exit 7 `Failed to connect to 127.0.0.1 port 47611 after 0 ms: Could not
  connect to server`.
- `--http3` (QUIC raced with TCP) with `--interface bogus0`: exit 45 too.
- curl 8.18.0's error message (the `curl: (45)` line) is the first `failf` of the bind, `Could not bind to
  'bogus0' with errno 0: The operation completed successfully.` or `bind failed with errno 10048: Address
  already in use`, for QUIC and for TCP alike (`--interface bogus0 http://…` measured the same); curl
  8.21.0, the TCP reference, ends a TCP bind failure with the `Failed to connect to` line instead (BL-600).
- An over-long `ifhost!` device name is refused at setopt by 8.18.0 (exit 43 `setopt 0x274e got bad
  argument`), and at connect by 8.21.0; the console already refuses a malformed value before connecting.

## Decision

1. The local-address choice moves out of `LocalBindingTcpDialer` into `LocalBindingAddressChooser`, which
   both the TCP dialer and `QuicDialer` use; `TcpConnector` builds one from its `LocalBinding` and puts it
   on `QuicDialRequest.LocalBinding` (the CONNECT-UDP tunnel path leaves it null: its TCP dial to the proxy
   is already bound).
2. `IUdpChannelOpener` gains `OpenFrom(serverEndPoint, localEndPoint, localPortCount)`, mirroring
   `ITcpDialer.DialFromAsync`; `UdpChannelOpener` binds through `TcpDialer.BindLocalEnd`, so the port range
   is walked exactly as for TCP and a range with no free port is `LocalBindException(InterfaceFailed)`.
3. `QuicDialer` opens (and binds) the socket before it reports the trust anchors, as curl.se's build does,
   and closes it when the trust anchors cannot be read.
4. A failed bind moves on to the next address, as for TCP. Its failure is one line, reported and returned:
   `Failed to connect to <host> port <port> after <n> ms: <words>`, with exit 45 `Failed binding local
   connection end`, exit 43 `A libcurl function was given a bad argument`, or exit 7 `Could not connect to
   server` for a local address of the other family; no `QUIC connect to` line.
5. The returned message is that `Failed to connect to` line, not 8.18.0's `Could not bind to ...` text: the
   difference is the curl version's, not QUIC's (8.18.0 words TCP the same way), and the TCP path already
   matches 8.21.0, so QUIC's exit 45 and 43 read as the TCP path's do, in QUIC's `host port N` form.
   The `Could not bind to`, `Bind to local port N failed, trying next` and `Local port: N` `-v` lines are
   BL-1027's, for TCP and QUIC alike.

## Consequences

- `--http3` and `--http3-only` honour `--interface` and `--local-port`; nothing changes in `Curl.Console`,
  which already passes `LocalBinding` to `TcpConnector`.
- `QuicDialer`'s public constructor keeps its `localAddress`/`localPort` parameters, which bind every socket
  without the range walk; the console does not use them.
- Off Windows the interface lookup finds interfaces (ADR-0110), so `--interface lo` binds its address for QUIC
  too; the OpenSSL QUIC build's bind lines were not measured (Fedora's curl in Docker could, if a difference
  is ever suspected).
