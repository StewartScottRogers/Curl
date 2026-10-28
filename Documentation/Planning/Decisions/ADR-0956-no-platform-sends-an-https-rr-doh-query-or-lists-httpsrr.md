# ADR-0956: No platform sends an HTTPS-RR DoH query or lists HTTPSRR

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3914 (CGR-157, Linux half; amends ADR-0856)
- Decided by Claude under Stewart's delegation.

## Context

ADR-0856 turned `TcpConnector.AsksHttpsRecords` off on Windows, where the Schannel reference build has no
`HTTPSRR` and sends only the A and AAAA DoH queries, and kept it on elsewhere, where `curl -V` listed `HTTPSRR`.
CGR-157 also reports that the Linux reference sent only the A and AAAA queries for
`curl -sS -k --doh-url ... --doh-insecure https://foo.test:21805/x`, where Curl off Windows sent a third,
`_21805._https.foo.test` type 65.

Measured (WSL Ubuntu, 2026-10-28): `curl 8.18.0 libcurl/8.18.0 OpenSSL/3.5.5` (8.18.0-1ubuntu2.8) lists
`Features: alt-svc AsynchDNS brotli GSS-API HSTS HTTP2 HTTPS-proxy IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL threadsafe TLS-SRP UnixSockets zstd`:
no `HTTPSRR`. The gap report's own Linux measurement of the repro shows only the A and AAAA POSTs. curl builds
`USE_HTTPSRR` only on request, so a default OpenSSL build has none.

## Decision

- `TcpConnector.AsksHttpsRecords` defaults to `false` on every platform; a direct connect's `https://` origin
  under `--doh-url` asks only for its addresses. The HTTPS-RR path stays, reached when a caller sets the property.
- `CurlVersionText.FeaturesLine` (Linux and macOS) no longer lists `HTTPSRR`, and `UpstreamUnixFeatures` drops it,
  so upstream case 2100's `%if HTTPSRR` protocol is no longer expected on any platform.
- `ManagedCurlVersion` (the engine's `curl_version_info` features off Windows) drops `HTTPSRR` with the line.
- Upstream case 2117 (`<features>` HTTPSRR) leaves the ratchet: upstream skips it on a build without HTTPSRR.
- `Cgr157Tests` pins the default on every platform; `Cgr157LinuxTests` pins it off Windows.

## Consequences

Curl's DoH traffic and `curl -V` match both reference builds. Libcurl's `CurlVersion` still knows the name `HTTPSRR`
(with no feature bit) so it can describe any build's features; it lists only what `ManagedCurlVersion` reports.
