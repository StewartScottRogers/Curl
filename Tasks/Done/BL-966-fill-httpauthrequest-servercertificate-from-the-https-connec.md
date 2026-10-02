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
completed: 2026-10-02
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

- [x] A `Curl.Protocol.Http.UnitTests` test pins that an HTTPS transfer's `HttpAuthRequest.ServerCertificate` is the server certificate's DER.
- [x] A test pins that a plain HTTP transfer's `HttpAuthRequest.ServerCertificate` is empty.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-915.
- Plan: once connected, `HttpRequestPlan.TakeServerCertificateFrom(connect)` sets the origin `AuthRequest.ServerCertificate` to the first peer certificate for an `https` URL; retry plans carry it forward, so every challenge answer (where Negotiate runs) sees it. The pre-connect first value has none; curl sends Negotiate only after a challenge.
- Decision (ADR-0341): the proxy request never carries a certificate and an `http` URL through an HTTPS proxy sends none, because curl's `Curl_ssl_get_channel_binding` reads only the origin TLS filter (`Curl_cft_ssl`), never `Curl_cft_ssl_proxy`. Not measured: it needs an MIT build and a realm behind an HTTPS proxy, which the loopback harness lacks.
- Tests: `ExecuteAsync_HttpsChallenge_AsksWithTheServerCertificateDer`, `ExecuteAsync_HttpChallenge_AsksWithNoServerCertificate`. Build clean, fast tests green, `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. HTTPS transfers hand the origin's server certificate DER to the authenticator, so hand-built Negotiate sends tls-server-end-point channel bindings
