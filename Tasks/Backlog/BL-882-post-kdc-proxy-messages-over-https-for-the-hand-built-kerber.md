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
completed:
---
# BL-882 — Post KDC-PROXY-MESSAGEs over HTTPS for the hand-built Kerberos

## Goal

An `https://` `kdc` entry in `krb5.conf` is reached in production: `Curl.Networking.UnitLibrary` implements `IKerberosKdcProxyTransport` and `Curl.Console`'s `HandBuiltKerberosSources` passes it to `KerberosKdcClient`.

## Context

- Follow-up from BL-827, which added the seam `IKerberosKdcProxyTransport.PostAsync(host, port, path, body)` and `KerberosKdcProxyMessage` to `Curl.Kerberos.UnitLibrary`; until this lands the production client passes no proxy transport, so `https://` KDCs are still skipped.
- MIT `src/lib/krb5/os/sendto_kdc.c` (`make_proxy_request`, `service_https_write`, `service_https_read`): TLS with the proxy's certificate verified against the host name, then `POST /<path> HTTP/1.0`, `Host:`, `Content-type: application/kerberos`, `Content-length:`, body; the reply's body is the `KDC-PROXY-MESSAGE`. A non-200 status or TLS failure is an `IOException` (the sender then tries the realm's next KDC).
- Reuse the existing TLS and connector seams in `Curl.Networking.UnitLibrary` (as `KerberosKdcSocketTransport` does for UDP/TCP); no `HttpClient`.

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` drive the new transport over a fake connection and pin the request bytes (request line, headers, body) and the reply-body extraction, plus an `IOException` for a non-200 status.
- [ ] `Curl.Console.UnitTests` show `HandBuiltKerberosSources` giving `KerberosKdcClient` the proxy transport.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Networking.UnitLibrary` and `Curl.Console` with no failing member.

## Notes

## Log

- 2026-09-29: Created.
