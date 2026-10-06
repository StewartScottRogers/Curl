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
completed: 2026-10-01
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

- [x] `-v` with a matching `sha256//` pin prints the `public key hash:` line where each build prints it; a file pin prints none.
- [x] `-v` with a mismatch prints each build's mismatch lines (twice for Schannel, once for OpenSSL), pinned per platform by tests.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured again 2026-10-01, curl 8.21.0 Schannel, `Record-CurlExchange.ps1 -Port 18877 -Tls -TlsPublicKeyFile k.pem`
  with `-v -k -o NUL --pinnedpubkey <pin>`: the hash line names the server key's hash on a mismatch too
  (`*  public key hash: sha256//<server key>`), then the mismatch line twice, `* closing connection #0`.
  Running Curl through the same script (`-Curl` a `.cmd` wrapping `dotnet run --project Curl.Console --no-build --`)
  now gives the same bytes, except that a refusal still lacks `* ALPN: curl offers http/1.1` before the hash:
  no failed handshake prints its handshake lines today. Filed as BL-1149, with the OpenSSL build's certificate
  details on a refusal.
- Design (ADR-0336): `Judge` records the hash on `PeerVerification` once the certificate is accepted
  (`PinnedPublicKey.ReportedHash`); a completed handshake carries it as `TlsHandshakeEvent.PinnedPublicKeyHash`,
  which `TransferEventInfoText` places between the ALPN lines (Schannel) or after the verify result (OpenSSL);
  a refusal reports info lines through `PeerVerification.ReportPinnedPublicKeyRefusal` in both providers.
  QUIC sets no hash yet.
- Tests: `SslStreamTlsProviderTests.PinnedPublicKey`, `HandBuiltTlsProviderTests.PinnedPublicKey`,
  `PinnedPublicKeyTests.ReportedHash_*`, `PeerVerificationTests.ReportPinnedPublicKeyRefusal_*`,
  `TransferEventInfoTextPinnedPublicKeyTests`. Curl.Console.UnitTests is outside `touches`, so no console test.
- Quality: Networking 100/100 (1130 members, 0 failing), Output 100/100 (444, 0), Abstractions 100/100 (208, 0).

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. -v prints --pinnedpubkey's public key hash line where each build does and its mismatch lines (twice Schannel, once OpenSSL)
