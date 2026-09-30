---
id: BL-616
title: Send a PROXY protocol v1 header for --haproxy-protocol and --haproxy-clientip
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-612]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-30
---
# BL-616 — Send a PROXY protocol v1 header for --haproxy-protocol and --haproxy-clientip

## Goal

With `--haproxy-protocol`, the first bytes on the connection are the HAProxy PROXY protocol v1 line (`PROXY TCP4 <src> <dst> <sport> <dport>\r\n`, or `TCP6`), with `--haproxy-clientip` replacing the source address, exactly as curl 8.21.0 writes it, before any TLS or HTTP bytes.

## Context

- Conformance audit 2026-09-28, row 16 (Major). Options: BL-612.
- The line needs the connection's local and remote endpoints (`ConnectResult`), so it belongs in `Curl.Networking.UnitLibrary/TcpConnector.cs` after the dial and before the TLS provider.
- `Record-CurlExchange.ps1` records everything curl sends, so the line shows at the start of `request.bin`.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `--haproxy-protocol http://127.0.0.1:<P>/`, the same to `[::1]`, `--haproxy-clientip 1.2.3.4`, and `--haproxy-protocol -k https://...` with `-Tls`; `request.bin` copied into Notes.
- [x] `Curl.Networking.UnitTests` pin the line for IPv4, IPv6 and a client IP, written before the TLS handshake.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-09-30 against curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1`; first line of `request.bin` for each (ephemeral ports vary):

| Command | First bytes of `request.bin` |
| --- | --- |
| `--haproxy-protocol http://127.0.0.1:48616/` | `PROXY TCP4 127.0.0.1 127.0.0.1 52929 48616\r\n` then `GET / HTTP/1.1` |
| `--haproxy-protocol http://[::1]:48617/` (`-ListenAddress ::1`) | `PROXY TCP6 ::1 ::1 52934 48617\r\n` |
| `--haproxy-clientip 1.2.3.4 http://127.0.0.1:48618/` (no `--haproxy-protocol`) | `PROXY TCP4 1.2.3.4 1.2.3.4 52946 48618\r\n` |
| `--haproxy-protocol --haproxy-clientip 1.2.3.4 ...` | `PROXY TCP4 1.2.3.4 1.2.3.4 52952 48619\r\n` |
| `--haproxy-clientip 1.2.3.4 --no-haproxy-protocol ...` | `PROXY TCP4 1.2.3.4 1.2.3.4 62677 48650\r\n` (the client IP turns it on regardless) |
| `--haproxy-clientip 2001:db8::1 ...` | `PROXY TCP6 2001:db8::1 2001:db8::1 50467 48620\r\n` |
| `--haproxy-clientip not-an-ip` / `1.2.3` / `01.2.3.4` / `::ffff:1.2.3.4` / `[::1]` | `PROXY TCP6 <value> <value> <sport> <dport>\r\n`, value verbatim |
| `--haproxy-clientip ""` | exit 2, `option --haproxy-clientip: blank argument where content is expected` (already the parser's, BL-612) |
| `--haproxy-protocol --interface 127.0.0.1 http://127.0.0.2:48640/` | `PROXY TCP4 127.0.0.1 127.0.0.2 50276 48640\r\n` (source is local, destination remote) |
| `--haproxy-clientip 9.9.9.9 --interface 127.0.0.1 http://127.0.0.2:48641/` | `PROXY TCP4 9.9.9.9 9.9.9.9 50284 48641\r\n` (client IP replaces both) |
| `--haproxy-protocol --unix-socket <path> http://localhost/` (with or without a client IP) | `PROXY UNKNOWN\r\n` |
| `--haproxy-protocol -k https://127.0.0.1:48622/` with `-Tls` | exit 35 `Recv failure: Connection was reset`, `request.bin` empty: the recorder's TLS server rejects the PROXY line |
| `--haproxy-protocol -k https://127.0.0.1:48637/` against the plain server | `PROXY TCP4 127.0.0.1 127.0.0.1 55520 48637\r\n` then byte 22, the ClientHello: the line precedes TLS |
| `-v --haproxy-protocol ...` | no extra `-v` line |

Decisions (all measured except the proxy case, no ADR needed):
- The client IP goes in verbatim for source and destination; `TCP4` only when it is a strict dotted quad as curl's `inet_pton` reads one (four parts, each 0-255, no leading zero), else `TCP6`.
- IPv6 addresses are written without a scope id, as `inet_ntop` writes them.
- Through a proxy the line is written once the tunnel is open, from the proxy socket's ends, before the target's TLS, as curl's filter chain puts the haproxy filter above the proxy filters and below TLS (from curl's `lib/connect.c`, not measured: the recorder has no proxy mode). A forward (non-tunnel) proxy connection gets it too.
- Written in `TcpConnector.SecureWhenAskedAsync`, the one step every direct, Unix-socket, HTTP-tunnel and SOCKS connect passes through; a reused pooled connection is not re-sent it, as curl sends it once per connection.
- Our `curl.exe` recorded the same way gives `PROXY TCP4 127.0.0.1 127.0.0.1 56048 48660\r\n` then `GET`, `PROXY TCP6 2001:db8::1 2001:db8::1 ...` over `[::1]`, and the line then byte 22 for https.
- `--ai-help` needs no change: both options were already listed (BL-612) and no option changed.
- `HaproxyProtocolHeader.IsOctet` first measured complexity 12; rewritten as a `byte.TryParse` round trip (complexity within 10).

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. --haproxy-protocol and --haproxy-clientip send curl 8.21.0's PROXY v1 line first on each connection, before TLS
