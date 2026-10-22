---
id: BL-2920
title: Send the DoH HTTPS-RR query for an http-family URL as curl 8.22.0's HTTPSRR build does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/DohDnsResolver.cs, Curl.Networking.UnitTests/DohDnsResolverHttpsRecordQueryTests.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitLibrary/IHttpsOriginDnsResolver.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2920 — Send the DoH HTTPS-RR query for an http-family URL as curl 8.22.0's HTTPSRR build does

## Goal

With `--doh-url`, Curl sends the DoH HTTPS (type 65) query for an http-family URL as well as A and AAAA, first, as curl 8.22.0 built with HTTPSRR does, so upstream case 2100's `%if HTTPSRR` protocol matches.

## Context

Found by BL-2837 (2026-10-21). Upstream test 2100 (no `--ech`) expects three DoH POSTs: the HTTPS query for `_8989._https.foo.example.com` (Content-Length 47) first, then A and AAAA, then the GET. Curl's `DohDnsResolver.ResolveHttpsRecordAsync` is asked only under `--ech` (ADR-0312). Read `lib/doh.c` `Curl_doh` (`USE_HTTPSRR`, `DOH_SLOT_HTTPS_RR`) at tag `curl-8_22_0` and measure the platform's curl with `Record-CurlExchange.ps1` before pinning: whether the Schannel build on Windows and the OpenSSL build send it, and for which schemes. Record the decision in an ADR. The `-v`/`--trace-config doh` lines for the third query must match too (`DohTraceLines`, `DohSubTransferEvents`).

## Acceptance criteria

- [x] An ADR records what curl 8.22.0 sends (measured) and on which builds.
- [x] A unit test in `Curl.Networking.UnitTests/DohDnsResolverHttpsRecordQueryTests.cs` pins the HTTPS query's bytes and its place before the A and AAAA queries for an https URL on a non-default port.
- [x] `--ai-help` is unchanged or kept right if any option's behaviour text changes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage.

## Notes

- Upstream (`lib/cf-https-connect.c`, `lib/vdns/doh.c`, `lib/vdns/cf-dns.c` at `curl-8_22_0`): the HTTPS-RR resolve is started by the HTTPS-CONNECT filter for every `https://` origin under `USE_HTTPSRR`, `--ech` or not, before the address resolve, as its own DoH resolve; not for an IP literal. Measured: the only local curl 8.22.0 (Git for Windows, Schannel, Debug) lacks the `HTTPSRR` feature and sends none, which is why upstream guards it with `%if HTTPSRR`; Curl's `--version` claims `HTTPSRR` on both builds, so it behaves as an HTTPSRR build everywhere. ADR-0727.
- Design: new `IHttpsOriginDnsResolver` (Networking); `TcpConnector.LookUpAsync` calls its `ResolveHttpsOriginAsync(host, port)` for a direct connect to an `https://` origin; `DohDnsResolver` posts the HTTPS query first and waits for it (so it is first at the server every time), then A and AAAA, and keeps the record so `--ech` asks nothing again.
- `touches` widened to `Curl.Networking.UnitLibrary/TcpConnector.cs` (the resolve call that knows the port and scheme) and the new `IHttpsOriginDnsResolver.cs`; no task in Doing on `origin/work/dark-factory` (BL-2892, BL-2913, BL-2926) names either. The TcpConnector tests live in `DohDnsResolverHttpsRecordQueryTests.cs`.
- `CURL_UPSTREAM_CASE=2100`: the HTTPS POST is now first; the case's next difference is `Content-Length: 47` vs `46`, because the harness's `%HTTPSPORT` has four digits where upstream's hard-coded length assumes five. Filed BL-2953. The HTTPS query's `-v`/`--trace-config doh` lines are not written yet: filed BL-2954.
- `--ai-help`: no option text describes the DoH queries, so it is unchanged.
- `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 0 failing members. Fast tests: all passed.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: a281346a 423467d0 74c730bd 7bd8d990.
- 2026-10-21: Doing -> Done. --doh-url sends the HTTPS-RR query first, before A and AAAA, for a direct https:// origin as curl 8.22.0's HTTPSRR build does (ADR-0727)
