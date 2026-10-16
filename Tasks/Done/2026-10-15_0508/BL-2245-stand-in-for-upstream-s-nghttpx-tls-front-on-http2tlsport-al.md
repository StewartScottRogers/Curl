---
id: BL-2245
title: Stand in for upstream's nghttpx TLS front on %HTTP2TLSPORT (ALPN h2) so cases 359, 2403, 2404 and 2406 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2230]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Conformance.Http2Server.UnitLibrary, Curl.Conformance.Http2Server.UnitTests]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2245 — Stand in for upstream's nghttpx TLS front on %HTTP2TLSPORT (ALPN h2) so cases 359, 2403, 2404 and 2406 run

## Goal

`%HTTP2TLSPORT` has a value and a connection to it speaks HTTP/2 over TLS (`test-localhost.pem`, ALPN `h2`) through the BL-2244 server, so cases 359, 2403, 2404 and 2406 run.

## Context

Split out of BL-2230 (2026-10-14): BL-2230 puts the h2c server (BL-2244) on `%HTTP2PORT`; this task adds the TLS front. Follow how the runner already fronts sws with TLS for `%HTTPSPORT` in `Curl.Conformance.UnitLibrary` (certificate directory, `test-localhost.pem`), adding ALPN `h2` so curl negotiates HTTP/2. Upstream's nghttpx listens on both ports (`tests/servers.pm` at curl-8_22_0).

## Acceptance criteria

- [x] `%HTTP2TLSPORT` is substituted when a certificate directory is named; a unit test pins a TLS connection to it negotiating ALPN `h2` and a GET relayed to sws.
- [x] Cases 359, 2403, 2404 and 2406 are no longer screened out; each that passes is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, each that fails is named in Notes with a follow-up task per distinct cause.
- [x] `Measure-CodeQuality.ps1` shows no member of the changed libraries outside a gate; the fast tests are green; the changed libraries' `CLAUDE.md` files are updated.

## Notes

- Design (ADR-0496, decided by Claude under Stewart's delegation): `%HTTP2TLSPORT` is 9018, set only when a certificate directory is named, like the other stunnel-fronted ports. `UpstreamHttp2ServerFactory.CreateTls` builds `Http2TlsServerConnector` (Http2Server library): `TlsServerStream` offering ALPN `h2` alone, then the BL-2244 `Http2ServerSession`, now relaying with `X-Forwarded-Proto: https` (a `forwardedProto` parameter, `http` for h2c). `UpstreamHttp2Server` gained an optional `CreateTlsServer`; `InFrontOf` puts it on 9018 only with a certificate, which the runner loads from `%CERTDIR/certs/test-localhost.pem` only for a case naming the `http/2` server.
- Tests: `Http2TlsServerConnectorTests` pins an SslStream client negotiating `h2` and a GET relayed to the backend with `X-Forwarded-Proto: https`, and a plain-text client being closed; the runner tests and `UpstreamHttp2ServerTests` pin the port routing and substitution.
- Measured on Windows (2026-10-14): none of the four skips for the server or `%HTTP2TLSPORT` any more. 2403 and 2404 skip for `http/2` (absent on Windows, ADR-0495); 359 and 2406 skip for `Debug`. None can run on this lane (no Linux .NET), so none is listed and none fails here. Follow-ups: BL-2263 (Debug stand-in and `CURL_ALTSVC_HTTP` for 359 and 2406), BL-2264 (measure and list 2403, and record 2404's `<tool>` skip, on Linux and macOS CI).
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary,Curl.Conformance.Http2Server.UnitLibrary`: both 100% line and branch, 0 failing members. Fast tests: all green.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. %HTTP2TLSPORT (9018) serves HTTP/2 over TLS with ALPN h2 through the nghttpx stand-in; cases 359, 2403, 2404 and 2406 no longer skip for the server or port
