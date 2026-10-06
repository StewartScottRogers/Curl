---
id: BL-882
title: Post KDC-PROXY-MESSAGEs over HTTPS for the hand-built Kerberos
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-827]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-882 — Post KDC-PROXY-MESSAGEs over HTTPS for the hand-built Kerberos

## Goal

An `https://` `kdc` entry in `krb5.conf` is reached in production: `Curl.Networking.UnitLibrary` implements `IKerberosKdcProxyTransport` and `Curl.Console`'s `HandBuiltKerberosSources` passes it to `KerberosKdcClient`.

## Context

- Follow-up from BL-827, which added the seam `IKerberosKdcProxyTransport.PostAsync(host, port, path, body)` and `KerberosKdcProxyMessage` to `Curl.Kerberos.UnitLibrary`; until this lands the production client passes no proxy transport, so `https://` KDCs are still skipped.
- MIT `src/lib/krb5/os/sendto_kdc.c` (`make_proxy_request`, `service_https_write`, `service_https_read`): TLS with the proxy's certificate verified against the host name, then `POST /<path> HTTP/1.0`, `Host:`, `Content-type: application/kerberos`, `Content-length:`, body; the reply's body is the `KDC-PROXY-MESSAGE`. A non-200 status or TLS failure is an `IOException` (the sender then tries the realm's next KDC).
- Reuse the existing TLS and connector seams in `Curl.Networking.UnitLibrary` (as `KerberosKdcSocketTransport` does for UDP/TCP); no `HttpClient`.

## Acceptance criteria

- [x] `Curl.Networking.UnitTests` drive the new transport over a fake connection and pin the request bytes (request line, headers, body) and the reply-body extraction, plus an `IOException` for a non-200 status.
- [x] `Curl.Console.UnitTests` show `HandBuiltKerberosSources` giving `KerberosKdcClient` the proxy transport.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Networking.UnitLibrary` and `Curl.Console` with no failing member.

## Notes
- Plan: `Curl.Networking.KerberosKdcProxyHttpsTransport(IConnector, TimeSpan exchangeTimeout, TimeProvider)` implements `IKerberosKdcProxyTransport`; `HandBuiltKerberosSources` takes it and passes it to `KerberosKdcClient`; `CurlComposition.CreateSecurityContextFactory` builds it over the same connector the KDC's TCP route uses.
- Request mirrors MIT `make_proxy_request`: `POST /<path> HTTP/1.0`, `Host`, `Cache-Control: no-cache`, `Pragma: no-cache`, `User-Agent: kerberos/1.0`, `Content-type: application/kerberos`, `Content-Length`, body. An IPv6 host is bracketed in `Host` (MIT writes it bare, which is not a valid header; decided by Claude under Stewart's delegation).
- Reply mirrors MIT `service_https_read`: read until the proxy closes (HTTP/1.0), require an `HTTP/1.x 200` status line, body is everything after the first blank line. Replies over 1 MiB, with no blank line, or with another status are `IOException`s, so the sender tries the next KDC.
- TLS comes from `ConnectTarget(host, port, UseTls: true)`: the run's TLS provider verifies the certificate against the host name. No `PoolScheme`, so no ALPN and no pooling. Decided by Claude under Stewart's delegation: the run's TLS options apply as-is. That means `-k`/`--cacert` also apply to the KDC proxy, where MIT uses its own `http_anchors` setting. Reading `http_anchors` is not done yet; it would need its own task.
- Timeout: the whole exchange gets `CurlComposition.KdcProxyExchangeTimeout`, 10 s, the time MIT gives one KDC's stream connection.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. https:// kdc entries in krb5.conf are reached through KerberosKdcProxyHttpsTransport in production
