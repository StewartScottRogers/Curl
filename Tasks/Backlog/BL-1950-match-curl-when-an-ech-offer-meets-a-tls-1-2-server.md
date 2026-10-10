---
id: BL-1950
title: Match curl when an ECH offer meets a TLS 1.2 server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1950 — Match curl when an ECH offer meets a TLS 1.2 server

## Goal

`curl --ech ecl:<config> -k https://<tls-1.2-only server>/` exits as curl's OpenSSL build does, with the same error text.

## Context

Found by BL-1949. Upstream's test4001 offers ECH to a TLS 1.3 server that does not accept it, and curl's OpenSSL build exits 101 ("ECH required"). On macOS the conformance harness's SslStream server can serve only TLS 1.2, and there Curl's hand-built TLS client (`Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs`, `HandshakeTls13OrTls12Async`) falls back to TLS 1.2, completes the handshake, ignores the ECH offer and exits 52 (empty reply). RFC 9849 says a client whose ECH offer meets a server negotiating TLS 1.2 or below treats it as a rejection and aborts with `ech_required`; whether OpenSSL does so, or refuses earlier, must be measured. BL-1949 stopped holding test4001 to the passing list on macOS (`NeedsTls13ServerCases` in `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs`).

## Acceptance criteria

- [ ] Real curl (OpenSSL build with ECH) measured against a TLS 1.2-only loopback server with `--ech ecl:...`, its exit code and stderr recorded under Notes.
- [ ] A test in `Curl.Networking.UnitTests` pins Curl's exit code and error message for an ECH offer answered by a TLS 1.2 ServerHello, matching that measurement.

## Notes

## Log

- 2026-10-09: Created.
