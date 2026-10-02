# ADR-0352 — QUIC's UDP socket bind writes the TCP path's `-v` bind lines

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1078.
Amends ADR-0295's Decision 4, which left every bind line off QUIC's UDP socket until measured.

## Context

ADR-0292 (BL-1025) binds QUIC's socket as `--interface` and `--local-port` ask, and ADR-0295 (BL-1027)
writes libcurl's `bindlocal` `-v` lines for TCP dials. QUIC's chooser and `TcpDialer.BindLocalEnd` were
given `NoTransferEvents` because the lines had not been measured over QUIC.

They had been, in BL-1025's Notes: curl.se's Windows build, curl 8.18.0 (LibreSSL, ngtcp2), the reference
QUIC build, run with `--http3-only -v` through `Record-CurlExchange.ps1 -UdpSink`, writes after `Trying`:

| Case | Lines |
| --- | --- |
| `--interface 127.0.0.1 --local-port 41000-41010` | `Name '127.0.0.1' family 2 resolved to '127.0.0.1' family 2`, `Local port: 41000`, then the trust anchors |
| `--local-port 41000-41005`, 41000-41002 held | `Bind to local port N failed, trying next` for each, `Local port: 41003` |
| `--local-port 41000-41002`, all held | two `trying next` lines, `bind failed with errno 10048: Address already in use` |
| `--interface bogus0` | `Could not resolve host: ...`, `Could not bind to 'bogus0' with errno 0: ...` |
| `--interface ::1` to `127.0.0.1` | `Name '::1' family 2 resolved to '::1' family 23` |

each followed, on failure, by ADR-0292's `Failed to connect to` line. These are exactly the TCP path's lines.

## Decision

1. `IUdpChannelOpener.OpenFrom` and `OpenFromDeviceAsync` take an `ITransferEvents`; `UdpChannelOpener`
   hands it to `TcpDialer.BindLocalEnd` and `TcpDialer.BindDeviceOrLocalEndAsync`, as `TcpDialer` does.
2. `QuicDialer` passes the target's `Events` to them and to `LocalBindingAddressChooser.ChooseAsync`, so
   the lines land between `Trying` and the trust anchors (or the `Failed to connect to` line), as measured.
3. The texts stay `LocalBindLines`'s, so ADR-0295's choices hold for QUIC too: the resolve line names the
   bind host (8.18.0 named the URL's host, taken as that version's quirk), and off Windows the Linux errnos.

## Consequences

- `curl --http3 / --http3-only -v --interface ... --local-port ...` writes curl's bind lines.
- `IUdpChannelOpener` changed signature; its only implementations are `UdpChannelOpener` and the test fake.
- The OpenSSL QUIC build's lines were not measured; they are taken to match its TCP lines, as on Windows.

## Alternatives considered

- Re-measuring before wiring: BL-1025's table already covers every case BL-1078 names, with the same build.
- A default interface method writing nothing, as ADR-0295 did for `ITcpDialer`: no other implementation
  exists to keep compiling, so the plain parameter is simpler.
