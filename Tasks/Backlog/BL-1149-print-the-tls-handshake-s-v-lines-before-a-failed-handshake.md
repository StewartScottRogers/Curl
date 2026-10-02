---
id: BL-1149
title: Print the TLS handshake's -v lines before a failed handshake: Schannel's ALPN offer and OpenSSL's certificate details
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-877]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1149 — Print the TLS handshake's -v lines before a failed handshake: Schannel's ALPN offer and OpenSSL's certificate details

## Goal

`curl -v` on a TLS handshake that fails after the ClientHello prints the handshake lines curl prints
before its failure: the Schannel build's `* ALPN: curl offers ...`, and the OpenSSL build's
ALPN offer, `SSL connection using`, certificate details and verify result.

## Context

- Follow-up from BL-877 (ADR-0336). Both TLS providers report a `TlsHandshakeEvent` only for a
  completed handshake, so a failed one prints none of these lines.
- Measured 2026-10-01, curl 8.21.0 Schannel, `Record-CurlExchange.ps1 -Tls -TlsPublicKeyFile k.pem`,
  `-v -k --pinnedpubkey sha256//AAAA...=`: `* ALPN: curl offers http/1.1` comes right after
  `* schannel: using IP address, SNI is not supported by OS.` and before `*  public key hash:`;
  Curl prints the hash line straight after the `schannel:` line. Measure an exit 60 case too.
- curl 8.18.0's OpenSSL build prints the certificate details before the pin's hash line (BL-608 Notes).
- Code: `SslStreamTlsProvider` and `HandBuiltTlsProvider` failure paths, `TransferEventInfoText`.

## Acceptance criteria

- [ ] `-v -k --pinnedpubkey sha256//<wrong>` against `Record-CurlExchange.ps1 -Tls` prints `* ALPN: curl offers http/1.1` before `*  public key hash:` on Windows, pinned by a test.
- [ ] The OpenSSL build prints the certificate details before the hash line on a pin refusal, pinned by a test with each build's flag.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-10-01: Created.
