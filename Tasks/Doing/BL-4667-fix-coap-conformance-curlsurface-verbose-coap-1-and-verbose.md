---
id: BL-4667
title: Fix Coap conformance: CurlSurface:Verbose-Coap-1 and Verbose-Coaps-1
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapProtocolHandler.cs, Curl.Extended.Protocol.Coap.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed:
---
# BL-4667 — Fix Coap conformance: CurlSurface:Verbose-Coap-1 and Verbose-Coaps-1

## Goal

`curl -v coap://...` and `curl -v -k coaps://...` write the connection line every transfer's `-v` writes, and for `coaps` the DTLS line ADR-1222 names, before `* CoAP response 2.05`.

## Context

Found by BL-4627 (`Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/CoapTransferSurfaceCases.cs`,
`Verbose_SuccessfulGet_...`). Today `-v` on a successful coap or coaps GET writes only `* CoAP response 2.05` to standard
error. curl 8.22.0 writes `* Established connection to localhost (127.0.0.1 port N) from ...` for every transfer (as the
xmpp and mqtt5 transfer-surface cases measured; `Curl.Console/MultiStateTraceEvents.cs` writes it for TCP transfers). The
CoAP handler opens its UDP channel itself through `IDatagramConnector.OpenAsync` (`CoapProtocolHandler.cs` line ~58), so
nothing reports the connection. ADR-1222: after the DTLS handshake, coaps writes `* SSL connection using DTLSv1.2 / <cipher>`
on every platform, the line curl's OpenSSL build writes for a DTLS session (`SSL_get_version` gives `DTLSv1.2`).

## Acceptance criteria

- [ ] `CurlSurface:Verbose-Coap-1` and `CurlSurface:Verbose-Coaps-1` pass in `CoapTransferSurfaceCases`, and their lines are removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [ ] A focused unit test in `Curl.Extended.Protocol.Coap.UnitTests` pins the connection line, and the DTLS line for coaps (ADR-0482).
- [ ] `Curl.Extended.Protocol.Coap.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: c68daaae f35502d1 44307beb 914c30cc.
