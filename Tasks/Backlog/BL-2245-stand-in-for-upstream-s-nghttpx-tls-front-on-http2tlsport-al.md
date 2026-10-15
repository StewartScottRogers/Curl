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
completed:
---
# BL-2245 — Stand in for upstream's nghttpx TLS front on %HTTP2TLSPORT (ALPN h2) so cases 359, 2403, 2404 and 2406 run

## Goal

`%HTTP2TLSPORT` has a value and a connection to it speaks HTTP/2 over TLS (`test-localhost.pem`, ALPN `h2`) through the BL-2244 server, so cases 359, 2403, 2404 and 2406 run.

## Context

Split out of BL-2230 (2026-10-14): BL-2230 puts the h2c server (BL-2244) on `%HTTP2PORT`; this task adds the TLS front. Follow how the runner already fronts sws with TLS for `%HTTPSPORT` in `Curl.Conformance.UnitLibrary` (certificate directory, `test-localhost.pem`), adding ALPN `h2` so curl negotiates HTTP/2. Upstream's nghttpx listens on both ports (`tests/servers.pm` at curl-8_22_0).

## Acceptance criteria

- [ ] `%HTTP2TLSPORT` is substituted when a certificate directory is named; a unit test pins a TLS connection to it negotiating ALPN `h2` and a GET relayed to sws.
- [ ] Cases 359, 2403, 2404 and 2406 are no longer screened out; each that passes is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, each that fails is named in Notes with a follow-up task per distinct cause.
- [ ] `Measure-CodeQuality.ps1` shows no member of the changed libraries outside a gate; the fast tests are green; the changed libraries' `CLAUDE.md` files are updated.

## Notes

## Log

- 2026-10-14: Created.
