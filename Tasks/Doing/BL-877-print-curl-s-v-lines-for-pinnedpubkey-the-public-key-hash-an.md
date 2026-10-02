---
id: BL-877
title: Print curl's -v lines for --pinnedpubkey: the public key hash and the mismatch
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-608]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-877 — Print curl's -v lines for --pinnedpubkey: the public key hash and the mismatch

## Goal

`curl -v --pinnedpubkey ...` prints curl's `public key hash:` line for a `sha256//` pin and its
mismatch lines, byte for byte as each build does.

## Context

- Follow-up from BL-608 (ADR-0193), which made `--pinnedpubkey` fail with exit 90 but prints no `-v` line for it.
- Measured 2026-09-29 against `Record-CurlExchange.ps1 -Tls -TlsPublicKeyFile` (stderr in BL-608's Notes):
  - Schannel curl 8.21.0, `sha256//` pin: `*  public key hash: sha256//<base64>` (two spaces) after `* ALPN: curl offers http/1.1`
    and before `* ALPN: server did not agree on a protocol. Uses default.`; on a mismatch, after that line,
    `* SSL: public key does not match pinned public key` twice, then `* closing connection #0`, and no ALPN result line.
    A file pin prints no hash line.
  - OpenSSL curl 8.18.0: the hash line after `*  SSL certificate verification failed, continuing anyway!` (the
    certificate details are printed first), and the mismatch line once.
- Code: `Curl.Networking.UnitLibrary/PinnedPublicKey.cs` (`HashOf`), `ServerCertificateVerification.Judge`, the
  `TlsHandshakeEvent` in `Curl.Protocol.Abstractions.UnitLibrary`, and `Curl.Output.UnitLibrary`'s verbose writer.
  A mismatch today fails inside the handshake, so no `TlsHandshakeEvent` is reported for it.

## Acceptance criteria

- [ ] `-v` with a matching `sha256//` pin prints the `public key hash:` line where each build prints it; a file pin prints none.
- [ ] `-v` with a mismatch prints each build's mismatch lines (twice for Schannel, once for OpenSSL), pinned per platform by tests.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
