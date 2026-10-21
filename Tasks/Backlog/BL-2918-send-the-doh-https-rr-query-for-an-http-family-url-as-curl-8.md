---
id: BL-2918
title: Send the DoH HTTPS-RR query for an http-family URL as curl 8.22.0's HTTPSRR build does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/DohDnsResolver.cs, Curl.Networking.UnitTests/DohDnsResolverHttpsRecordQueryTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2918 — Send the DoH HTTPS-RR query for an http-family URL as curl 8.22.0's HTTPSRR build does

## Goal

With `--doh-url`, Curl sends the DoH HTTPS (type 65) query for an http-family URL as well as A and AAAA, first, as curl 8.22.0 built with HTTPSRR does, so upstream case 2100's `%if HTTPSRR` protocol matches.

## Context

Found by BL-2837 (2026-10-21). Upstream test 2100 (no `--ech`) expects three DoH POSTs: the HTTPS query for `_8989._https.foo.example.com` (Content-Length 47) first, then A and AAAA, then the GET. Curl's `DohDnsResolver.ResolveHttpsRecordAsync` is asked only under `--ech` (ADR-0312). Read `lib/doh.c` `Curl_doh` (`USE_HTTPSRR`, `DOH_SLOT_HTTPS_RR`) at tag `curl-8_22_0` and measure the platform's curl with `Record-CurlExchange.ps1` before pinning: whether the Schannel build on Windows and the OpenSSL build send it, and for which schemes. Record the decision in an ADR. The `-v`/`--trace-config doh` lines for the third query must match too (`DohTraceLines`, `DohSubTransferEvents`).

## Acceptance criteria

- [ ] An ADR records what curl 8.22.0 sends (measured) and on which builds.
- [ ] A unit test in `Curl.Networking.UnitTests/DohDnsResolverHttpsRecordQueryTests.cs` pins the HTTPS query's bytes and its place before the A and AAAA queries for an https URL on a non-default port.
- [ ] `--ai-help` is unchanged or kept right if any option's behaviour text changes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-21: Created.
