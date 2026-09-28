---
id: BL-441
title: Handshake with an HTTPS forward proxy through the proxy TLS provider in TcpConnector
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-442]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-441 — Handshake with an HTTPS forward proxy through the proxy TLS provider in TcpConnector

## Goal

A connection to an HTTPS proxy that is not a tunnel (`ConnectTarget { IsForwardProxy = true, UseTls = true }`, as `HttpProtocolHandler` and `TftpProtocolHandler` open it) is verified with the proxy's TLS options (`--proxy-insecure`, `--proxy-cacert`), not the target's (`-k`, `--cacert`), as ADR-0095 does for a tunnel.

## Context

- ADR-0095 routes the handshake to an HTTPS proxy through `TcpConnector`'s `proxyTlsProvider`, but only on the tunnel path (`OpenTunnelOverTlsAsync`). A forward-proxy target reaches the proxy with `UseTls: true` and, as read by BL-398, goes through the target's `tlsProvider`.
- ADR-0095 measured that curl 8.21.0 never lets `-k` or `--cacert` reach the proxy; check the forward case the same way (`curl -x https://p http://example.com/` against a TLS loopback proxy, `-k` alone then `--proxy-insecure`) before changing anything. BL-442 adds the `-Tls` recorder switch that makes this measurable.

## Acceptance criteria

- [x] The measured commands and results for `-k` and `--proxy-insecure` with an `https://` forward proxy are recorded under this task's Notes.
- [x] A test in `Curl.Networking.UnitTests` pins which `ITlsProvider` a forward-proxy `UseTls` target is handshaken through.
- [x] 100% line and branch coverage of the changed code; `dotnet build` clean, fast tests green.

## Notes

- Measured with curl 8.21.0 (Schannel, Git for Windows) and `Record-CurlExchange.ps1 -Port 18441 -Tls`
  (self-signed proxy certificate), response `HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello`:
  - `curl -sS -x https://127.0.0.1:18441 http://example.com/ -k` -> exit 60,
    `curl: (60) schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.`,
    nothing sent to the proxy.
  - `curl -sS -x https://127.0.0.1:18441 http://example.com/ --proxy-insecure` -> exit 0, stdout `hello`;
    the proxy received `GET http://example.com/ HTTP/1.1` with `Host: example.com`, `User-Agent: curl/8.21.0`,
    `Accept: */*`, `Proxy-Connection: Keep-Alive`.
  - `-k --proxy-insecure` together -> exit 0, same bytes.
  So a forward proxy is verified with the proxy's TLS options only, as ADR-0095 measured for a tunnel.
- Change: `TcpConnector.AuthenticateTargetAsync` picks the proxy's `ITlsProvider` when
  `ConnectTarget.IsForwardProxy` is set. The handshake is still reported through
  `IHandshakeReportingTlsProvider` when the chosen provider is one, as it was before (the forward
  proxy is the connection's server, so `-v` reports it as one); no new decision, so no ADR - this
  applies ADR-0095's measured rule to the forward case. `CurlComposition` already passes the
  proxy provider, so no change outside Curl.Networking.
- Test: `TcpConnectorTests.ConnectAsync_WithTls_HandshakesAForwardProxyThroughTheProxyTlsProviderAndATargetThroughTheOther`
  (both rows cover the new branch). Networking fast tests: 743 passed, 6 skipped.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. An HTTPS forward proxy is handshaken through the proxy TLS provider, so --proxy-insecure and --proxy-cacert verify it and -k does not
