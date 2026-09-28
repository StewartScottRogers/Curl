---
id: BL-753
title: Measure and match the ALPN offer in the handshake with an HTTPS proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-490]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-753 — Measure and match the ALPN offer in the handshake with an HTTPS proxy

## Goal

The TLS handshake with an HTTPS proxy (`-x https://...`, and an HTTPS forward proxy) offers through ALPN exactly what the platform's curl 8.21.0 build offers there, so `-v` prints the same `ALPN:` lines for the proxy handshake.

## Context

- BL-490 / ADR-0124 made the origin handshake of an `https://` transfer offer `http/1.1` (none under `--no-alpn`) but left the proxy handshakes offering nothing, because curl's proxy ALPN was not measured.
- Where: `Curl.Networking.UnitLibrary/TcpConnector.cs` (`ApplicationProtocolsFor`, and the `AuthenticateAsync` call for the proxy passing `applicationProtocols: []`).
- Measure with `Record-CurlExchange.ps1 -Tls` as the proxy (`curl -v --proxy-insecure -x https://127.0.0.1:P http://example.test/`, and with `-p` to tunnel), on Windows (Schannel) and through WSL (OpenSSL, `-ListenAddress`), with and without `--no-alpn`.

## Acceptance criteria

- [ ] The measured `-v` lines for the proxy handshake, per build, are copied into Notes.
- [ ] Tests on `TcpConnector` pin the protocols offered to an HTTPS proxy and an HTTPS forward proxy as measured, and with `--no-alpn` if curl applies it to the proxy.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
