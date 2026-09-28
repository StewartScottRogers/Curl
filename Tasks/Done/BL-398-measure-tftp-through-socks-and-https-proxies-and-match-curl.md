---
id: BL-398
title: Measure tftp:// through SOCKS and HTTPS proxies and match curl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-345]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests, Documentation/Planning/Decisions/ADR-0096-tftp-through-socks-and-https-proxies-fails-as-the-reference-build-fails.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-398 — Measure tftp:// through SOCKS and HTTPS proxies and match curl

## Goal

`tftp://` with a SOCKS (`--socks5`, `--socks4`, …) or HTTPS proxy behaves as curl 8.21.0's Schannel build does, never sending TFTP datagrams around a proxy curl would use.

## Context

- ADR-0056 rule 4 covers only an HTTP proxy; BL-345 implemented that. For every other `ProxyKind`, `TftpProtocolHandler` still opens the datagram channel straight to the server.
- ADR-0056 rule 6 (the ADR-0053 guard in `Curl.Console`) may already refuse some of these with exit 4 before the handler runs; check what reaches the handler.
- Measure with `Record-CurlExchange.ps1` (for SOCKS, a loopback listener recording the greeting) before pinning anything.

## Acceptance criteria

- [x] The measured commands, bytes, stderr and exit codes for `--socks5`, `--socks4` and an `https://` proxy with `tftp://example.com/f` are recorded under this task's Notes.
- [x] A test in `Curl.Protocol.Tftp.UnitTests` pins each measured outcome the handler is responsible for.
- [x] 100% line and branch coverage of the changed code; `dotnet build` clean, fast tests green.

## Notes

Measured 2026-09-27 with curl 8.21.0 (x86_64-w64-mingw32, Schannel) and `Record-CurlExchange.ps1` on port 18398 (outputs under `%TEMP%\bl398`):

| Command | Proxy received | stderr | exit |
| --- | --- | --- | --- |
| `curl -sS --socks5 127.0.0.1:18398 tftp://example.com/f` | nothing (0 bytes) | `curl: (97) Send failure: Socket is not connected` | 97 |
| `curl -sS --socks4 127.0.0.1:18398 tftp://example.com/f` | nothing | the same | 97 |
| `--socks4a`, `--socks5-hostname`, `-x socks5://127.0.0.1:18398`, and `--socks5`/`--socks4` with `tftp://example.com/` | nothing | the same | 97 |
| `curl -sS -k -x https://127.0.0.1:18398 tftp://example.com/f` (plain TCP listener) | a TLS ClientHello (ALPN `http/1.1`) | `curl: (35) schannel: failed to receive handshake, SSL/TLS connection failed` | 35 |
| `curl -sS --proxy-insecure -x https://127.0.0.1:18398 tftp://example.com/f` (TLS listener, reply `200`, `204` or `101 Upgrade: connect-udp`) | inside TLS: `GET https://127.0.0.1:18398/.well-known/masque/udp/example.com/69/ HTTP/1.1`, `Host: 127.0.0.1:18398`, `User-Agent: curl/8.21.0`, `Proxy-Connection: Keep-Alive`, `Connection: Upgrade`, `Upgrade: connect-udp`, `Capsule-Protocol: ?1`, empty line | `curl: (7) bind() failed; Invalid arguments` | 7 |
| the same, reply `403` | the same request | `curl: (7) CONNECT-UDP tunnel failed, response 403` | 7 |
| the same, TLS listener closing without a reply (also with `tftp://example.com/`) | the same request | `curl: (56) schannel: server closed abruptly (missing close_notify)` | 56 |
| `curl -sS -x http://127.0.0.1:18398 tftp://example.com/f`, reply `403` / `407` / `302` / `garbage` | the BL-330 request | `curl: (7) CONNECT-UDP tunnel failed, response 403` (`407`, `302`, `0`) | 7 |
| the same, reply `204` | the same | `curl: (7) bind() failed; Invalid arguments` | 7 |
| the same, closing without a reply | the same | `curl: (56) Proxy CONNECT aborted` | 56 |

`-v` for SOCKS shows `Trying 127.0.0.1:18398...`, `Send failure: Socket is not connected`, `Failed to send SOCKS request`: libcurl sends the SOCKS greeting on the unconnected UDP socket, so nothing leaves.

Decisions (ADR-0096, decided by Claude under Stewart's delegation):
- SOCKS of any kind: exit 97 `Send failure: Socket is not connected`, nothing opened, before the file name check.
- HTTPS: as the HTTP proxy, over TLS (`ConnectTarget(UseTls: true) { IsForwardProxy = true }`, as `HttpProtocolHandler` reaches a forward proxy), `https://` in the request line.
- The reply is now read for HTTP and HTTPS proxies: this corrects BL-345's handler, which never read it, so a refusing proxy reported `bind()`. It is the same code path the HTTPS case needed; the measurement showed it, so it was fixed here rather than filed.
- A TLS close without a reply is reported `Proxy CONNECT aborted` (exit 56, matching curl's code; the Schannel text is the TLS layer's). The reply header block is not size-limited (unmeasured).

Scope:
- `touches` gained ADR-0096 and the ADR README (no task in Doing names them).
- The HTTPS measurement used a temporary copy of `Record-CurlExchange.ps1` in `%TEMP%` with an `SslStream` wrapper, not the committed script: BL-439 (in Doing) touches it. BL-442 files the `-Tls` switch properly.
- `Curl.Console` still builds `TftpProtocolHandler` without a proxy connector (BL-397), so in the product an HTTP or HTTPS proxy gives exit 7 `bind()` without sending the request; SOCKS is fully matched now.
- BL-441 filed: a forward-proxy `UseTls` target appears to handshake through the target's TLS provider in `TcpConnector`, not ADR-0095's proxy provider.

Results: `Curl.Protocol.Tftp.UnitTests` 121 passed; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Tftp.UnitLibrary` 100% line, 100% branch, worst CRAP 10; `dotnet build` 0 errors; fast tests green across all 17 test assemblies.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. tftp:// through a SOCKS proxy exits 97 with nothing sent, and through an HTTP or HTTPS proxy sends the MASQUE request (over TLS for HTTPS) and fails by the proxy's reply, as curl 8.21.0 Schannel does
