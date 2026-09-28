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
completed:
---
# BL-441 — Handshake with an HTTPS forward proxy through the proxy TLS provider in TcpConnector

## Goal

A connection to an HTTPS proxy that is not a tunnel (`ConnectTarget { IsForwardProxy = true, UseTls = true }`, as `HttpProtocolHandler` and `TftpProtocolHandler` open it) is verified with the proxy's TLS options (`--proxy-insecure`, `--proxy-cacert`), not the target's (`-k`, `--cacert`), as ADR-0095 does for a tunnel.

## Context

- ADR-0095 routes the handshake to an HTTPS proxy through `TcpConnector`'s `proxyTlsProvider`, but only on the tunnel path (`OpenTunnelOverTlsAsync`). A forward-proxy target reaches the proxy with `UseTls: true` and, as read by BL-398, goes through the target's `tlsProvider`.
- ADR-0095 measured that curl 8.21.0 never lets `-k` or `--cacert` reach the proxy; check the forward case the same way (`curl -x https://p http://example.com/` against a TLS loopback proxy, `-k` alone then `--proxy-insecure`) before changing anything. BL-442 adds the `-Tls` recorder switch that makes this measurable.

## Acceptance criteria

- [ ] The measured commands and results for `-k` and `--proxy-insecure` with an `https://` forward proxy are recorded under this task's Notes.
- [ ] A test in `Curl.Networking.UnitTests` pins which `ITlsProvider` a forward-proxy `UseTls` target is handshaken through.
- [ ] 100% line and branch coverage of the changed code; `dotnet build` clean, fast tests green.

## Notes

## Log

- 2026-09-27: Created.
