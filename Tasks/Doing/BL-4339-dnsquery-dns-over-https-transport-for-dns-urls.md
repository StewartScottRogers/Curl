---
id: BL-4339
title: DnsQuery: DNS-over-HTTPS transport for dns:// URLs
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4338]
touches: [Curl.Extension.DnsQuery.UnitLibrary/Transport/DnsQueryTransportSelection.cs, Curl.Extension.DnsQuery.UnitLibrary/Transport/Https, Curl.Extension.DnsQuery.UnitLibrary/CLAUDE.md, Curl.Extension.DnsQuery.UnitTests/Transport/Https]
requirement: none
created: 2026-10-29
completed:
---
# BL-4339 — DnsQuery: DNS-over-HTTPS transport for dns:// URLs

## Goal

`dns://` URLs can be queried over DNS-over-HTTPS (RFC 8484), selected through BL-4337's transport selection.

## Context

Split from BL-4150 (extension 50 of 63, ADR-0999, CLAUDE.md "Extensions"). Tested against RFC 1035 and RFC 4501, not against curl; never changes curl's behaviour. Never reference a `Curl.Protocol.*` library or another extension (in particular not `Curl.Extension.EncryptedDns`, whose DoT/DoQ resolvers and wire codec are a model to follow but must be written here); `Curl.Networking.UnitLibrary` and `Curl.Quic.UnitLibrary` are shared layers it may reference, as EncryptedDns does.

Builds on BL-4337's `Transport/DnsQueryTransportSelection.cs` (depends on BL-4338 only so the two never edit that file at once). The handler cannot use curl's HTTP stack (no `Curl.Protocol.Http` reference), so write a minimal HTTP/1.1 client here over `ProtocolHandlerServices`' connector and TLS provider: POST `application/dns-message` to the DoH URL (default path `/dns-query`, RFC 8484 section 4.1), or GET with base64url `?dns=`; decide which and how the DoH URL is given, and record it.

## Acceptance criteria

- [ ] DoH POSTs (or GETs, as decided) the RFC 8484 message with `Content-Type` and `Accept` `application/dns-message`, reads a 200 answer (chunked or Content-Length) and prints it; a non-200 or another content type fails with a recorded exit code; unit tests over a fake connection cite RFC 8484 sections.
- [ ] The extension's ADR (from BL-4337) or a new one records the DoH choices.
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes



## Log

- 2026-10-29: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: f908be11 f3bbf5c9 478f867b 3d836d5e.
