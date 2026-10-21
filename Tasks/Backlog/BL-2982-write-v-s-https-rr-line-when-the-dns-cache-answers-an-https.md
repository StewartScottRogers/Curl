---
id: BL-2982
title: Write -v's HTTPS-RR line when the DNS cache answers an https origin resolved through --doh-url
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/TcpConnectorHttpsRecordReportTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2982 — Write -v's HTTPS-RR line when the DNS cache answers an https origin resolved through --doh-url

## Goal

A second connect to an `https://` origin resolved through `--doh-url`, answered from the DNS cache, writes curl 8.22.0's `HTTPS-RR <host>:<port>:` line under `-v` as the first connect did, in its place beside `Host <host>:<port> was resolved.`.

## Context

Found by BL-2980 (ADR-0727's BL-2980 amendment). `TcpConnector.LookUpAsync` writes the line only after a fresh `IHttpsOriginDnsResolver.ResolveHttpsOriginAsync`; `ResolveNotingCacheAsync` answers a cached name without asking the resolver, so no line is written. curl 8.22.0's HTTPS DNS filter (`lib/vdns/cf-dns.c`) reports whenever it has an entry, cached or not. Derive whether curl writes it for a cached entry and in which order against `Hostname <host> was found in DNS cache`, from `lib/vdns/cf-dns.c` and `Curl_resolv_https` at tag `curl-8_22_0`, and record it in an ADR-0727 amendment.

## Acceptance criteria

- [ ] A unit test in `TcpConnectorHttpsRecordReportTests` pins the lines of a second connect to the same `https://` origin through an `IHttpsOriginDnsResolver`, as derived and recorded in an ADR-0727 amendment.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Networking.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
