---
id: BL-2972
title: Fail a DoH query on a non-2xx status as curl 8.22.0 does: [DoH] [A] error: HTTP status N
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2970]
touches: [Curl.Networking.UnitLibrary/DohResponseReader.cs, Curl.Networking.UnitLibrary/DohDnsResolver.Exchange.cs, Curl.Networking.UnitLibrary/DohTraceLines.ExchangeFailures.cs, Curl.Networking.UnitTests/DohResponseReaderTests.cs, Curl.Networking.UnitTests/DohDnsResolverTraceTests.ExchangeFailures.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2972 â€” Fail a DoH query on a non-2xx status as curl 8.22.0 does: [DoH] [A] error: HTTP status N

## Goal

A DoH response with a non-2xx status fails that query as curl 8.22.0's `DOH_HTTP_FAILED` does, without decoding the body, and under `-v --trace-config doh` writes `[DoH] [DoH] [A] error: HTTP status <n>` (`[AAAA]` for the AAAA query).

## Context

Split from BL-2970 (ADR-0738). Measured 2026-10-21 with the local curl 8.22.0 (Schannel): `Record-CurlExchange.ps1 -Tls -Port 18456 -Response 'HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\n\r\n' -CurlArgs -v,--trace-config,doh,--doh-insecure,--doh-url,https://127.0.0.1:18456/,-4,http://example.test:18454/` wrote `* [DoH] Connection #1 to host 127.0.0.1:18456 left intact`, then `* [DoH] [DoH] [A] error: HTTP status 500`, and exited 6. 8.21.0 ignored the status and decoded the empty body as `Too small`. `DohResponseReader` (BL-641) ignores the status today, so it must return it; `DohTraceLines.FailureLine` writes BL-2970's error lines and gains this one. Check whether a 3xx or 1xx counts as a failure in `lib/vdns/doh.c` (tag `curl-8_22_0`) before pinning.

## Acceptance criteria

- [ ] `DohDnsResolverTraceTests` pins `[DoH] [DoH] [A] error: HTTP status 500` for a 500 with an empty body, with no address resolved, and `DohResponseReaderTests` pins the status the reader returns.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Networking.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
