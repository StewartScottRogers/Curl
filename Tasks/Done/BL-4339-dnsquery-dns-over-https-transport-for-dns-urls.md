---
id: BL-4339
title: DnsQuery: DNS-over-HTTPS transport for dns:// URLs
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4338]
touches: [Curl.Extension.DnsQuery.UnitLibrary/Transport/DnsQueryTransportSelection.cs, Curl.Extension.DnsQuery.UnitLibrary/Transport/Https, Curl.Extension.DnsQuery.UnitLibrary/CLAUDE.md, Curl.Extension.DnsQuery.UnitTests/Transport/Https, Curl.Extension.DnsQuery.UnitTests/Transport/DnsQueryTransportSelectionTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-30
---
# BL-4339 — DnsQuery: DNS-over-HTTPS transport for dns:// URLs

## Goal

`dns://` URLs can be queried over DNS-over-HTTPS (RFC 8484), selected through BL-4337's transport selection.

## Context

Split from BL-4150 (extension 50 of 63, ADR-0999, CLAUDE.md "Extensions"). Tested against RFC 1035 and RFC 4501, not against curl; never changes curl's behaviour. Never reference a `Curl.Protocol.*` library or another extension (in particular not `Curl.Extension.EncryptedDns`, whose DoT/DoQ resolvers and wire codec are a model to follow but must be written here); `Curl.Networking.UnitLibrary` and `Curl.Quic.UnitLibrary` are shared layers it may reference, as EncryptedDns does.

Builds on BL-4337's `Transport/DnsQueryTransportSelection.cs` (depends on BL-4338 only so the two never edit that file at once). The handler cannot use curl's HTTP stack (no `Curl.Protocol.Http` reference), so write a minimal HTTP/1.1 client here over `ProtocolHandlerServices`' connector and TLS provider: POST `application/dns-message` to the DoH URL (default path `/dns-query`, RFC 8484 section 4.1), or GET with base64url `?dns=`; decide which and how the DoH URL is given, and record it.

## Acceptance criteria

- [x] DoH POSTs (or GETs, as decided) the RFC 8484 message with `Content-Type` and `Accept` `application/dns-message`, reads a 200 answer (chunked or Content-Length) and prints it; a non-200 or another content type fails with a recorded exit code; unit tests over a fake connection cite RFC 8484 sections.
- [x] The extension's ADR (from BL-4337) or a new one records the DoH choices.
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added `Curl.Extension.DnsQuery.UnitTests/Transport/DnsQueryTransportSelectionTests.cs` to touches: its test pins `Names` and the exit 2 message, which now list `https`; no other task in Doing names it.
- Decided (ADR-1148): `--dns-transport https`; the DoH URI is `https://<URL server>[:port]/dns-query`, port 443 by default, no option for another path; POST (no base64url, no URI limit; GET only helps caches) with `Content-Type` and `Accept` `application/dns-message` and `Connection: close`; HTTP/1.1 written in the extension (`Transport/Https`: `DnsHttpsTransport`, `DohRequest`, `DohResponseHead`, `DohResponseReader`); the random Message ID kept. Non-200 is exit 22, another content type or a malformed response exit 8, an early close exit 56.
- Verified: 214 DnsQuery unit tests and the whole fast run green (0 failed); Measure-CodeQuality for Curl.Extension.DnsQuery.UnitLibrary 100% line and branch, max complexity 10, 0 failing members; live `curl --dns-transport https dns://dns.google/example.com` printed the two A records.



## Log

- 2026-10-29: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: f908be11 f3bbf5c9 478f867b 3d836d5e.
- 2026-10-30: Doing -> Done. dns:// URLs are queried over DNS over HTTPS (RFC 8484) with --dns-transport https
