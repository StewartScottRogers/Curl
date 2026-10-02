---
id: BL-966
title: Fill HttpAuthRequest.ServerCertificate from the HTTPS connection so hand-built Negotiate sends channel bindings
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-915]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-966 — Fill HttpAuthRequest.ServerCertificate from the HTTPS connection so hand-built Negotiate sends channel bindings

## Goal

`HttpProtocolHandler` sets `HttpAuthRequest.ServerCertificate` to the DER of the connection's TLS server certificate (the first of `ConnectResult.PeerCertificates`), so hand-built Negotiate over HTTPS sends the `tls-server-end-point` channel bindings curl 8.18.0 with MIT sends (BL-832, BL-915, ADR-0234).

## Context

- BL-915 added `HttpAuthRequest.ServerCertificate` and `SecurityContextRequest.ServerCertificate` (Curl.Protocol.Abstractions), and `NegotiateHttpAuthenticator` and `HandBuiltKerberosSecurityContext` turn it into `KerberosGssContextOptions.ChannelBindings`. Nothing fills it yet, so bindings are zeros everywhere.
- `HttpProtocolHandler.cs` builds the origin `HttpAuthRequest` near line 253 and already reads `connect.PeerCertificates` near line 1723.
- For a proxy's `HttpAuthRequest` (`ProxyAuthRequestOf`), pass the proxy connection's certificate only when the proxy itself is HTTPS, as curl takes the bindings from the connection's first socket; measure with `Record-CurlExchange.ps1` before pinning if in doubt.
- Plain HTTP leaves it empty.

## Acceptance criteria

- [ ] A `Curl.Protocol.Http.UnitTests` test pins that an HTTPS transfer's `HttpAuthRequest.ServerCertificate` is the server certificate's DER.
- [ ] A test pins that a plain HTTP transfer's `HttpAuthRequest.ServerCertificate` is empty.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-915.

## Log

- 2026-09-29: Created.
- 2026-10-02: Backlog -> Doing.
