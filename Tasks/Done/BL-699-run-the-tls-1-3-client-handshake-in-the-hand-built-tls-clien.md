---
id: BL-699
title: Run the TLS 1.3 client handshake in the hand-built TLS client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-697, BL-698, BL-671, BL-672, BL-673]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Documentation/Planning/Decisions/ADR-0143-the-tls-1-3-client-handshake-takes-its-key-shares-and-extension-order-from-its-caller.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-699 — Run the TLS 1.3 client handshake in the hand-built TLS client

## Goal

The hand-built client runs a full TLS 1.3 client handshake as a message-level state machine (handshake bytes in and out per encryption level, so QUIC can carry them): key shares on X25519 and the NIST curves, HelloRetryRequest, the `TLS_AES_128_GCM_SHA256`, `TLS_AES_256_GCM_SHA384` and `TLS_CHACHA20_POLY1305_SHA256` suites, ALPN, SNI, CertificateVerify with RSA-PSS, ECDSA and Ed25519, the server Finished check, an optional client certificate, and the server chain handed to the verifier BL-695's ADR names.

## Context

- Design: BL-695's ADR. Builds on BL-697 (key schedule), BL-698 (messages); primitives X25519 (BL-671), Ed25519 (BL-672), ChaCha20-Poly1305 (BL-673) from `Curl.Cryptography.UnitLibrary` (add the reference here), the rest from the BCL.
- Reference: RFC 8448 section 3 (the full 1-RTT trace with its fixed ephemeral keys and randoms, which the tests inject) and section 5 (HelloRetryRequest). Alerts per RFC 8446 section 6 map to typed failures that the TCP and QUIC users turn into curl's exits (35 for a handshake failure, 60 for verification, as the existing `SslStreamTlsProvider` does).

## Acceptance criteria

- [x] `Curl.Tls.UnitTests` replay RFC 8448 section 3 as the client (injected keys and random), producing the trace's client flight byte for byte and accepting the server's, and complete a HelloRetryRequest exchange per section 5.
- [x] An in-memory TLS 1.3 server in the tests (built from the same pieces with a generated certificate) completes handshakes for every suite and key-share group and for each signature scheme, and a bad Finished, a bad CertificateVerify, an unsupported group and a downgrade sentinel each fail with the typed alert.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Built `Tls13ClientHandshake` (I/O-free: `Start()`, then `Receive(level, bytes)` returning
  a `Tls13HandshakeOutput` of bytes per level, installed traffic secrets, completion or a
  `TlsHandshakeFailure`), with `Tls13ClientSettings`, `Tls13ClientHelloBuilder`,
  `Tls13CipherSuite`, the key shares (`X25519KeyShare`, `EcdhKeyShare` with `NistCurve`,
  `FfdheKeyShare`), `ITlsRandomSource`/`SystemTlsRandomSource`, the
  `IServerCertificateVerifier` hand-off (`ServerCertificateChain`, `ServerCertificateVerdict`),
  `TlsCertificatePublicKey` for the CertificateVerify check, and `TlsSigningKey`
  (RSA-PSS, ECDSA, Ed25519) for client certificates. Added the
  `Curl.Cryptography.UnitLibrary` reference (X25519, Ed25519, finite-field DH).
- Decisions in ADR-0143 (Decided by Claude under Stewart's delegation): key shares come
  from `ITlsRandomSource.CreateKeyShare`, because importing a P-256 private key without its
  public point is not portable; the ClientHello extension order is data in the settings,
  so RFC 8448's hellos and BL-708's profiles are settings rather than code; `padding`
  follows the NSS/BoringSSL 512-byte rule the section 5 trace shows; NIST points are checked
  on the curve by hand. Reason: on Windows an off-curve point makes
  `ECDiffieHellman.Create` throw `PlatformNotSupportedException`, not
  `CryptographicException` (measured 2026-09-28). After completion only NewSessionTicket is
  taken. KeyUpdate is left to BL-700.
- Touches: added the ADR-0143 file and `Documentation/Planning/Decisions/README.md` to
  record the decision. No task in Doing names either.
- RFC 8448 sections 3 and 5 replay byte for byte: both ClientHellos, both client
  Finished messages, and every handshake and application secret, the exporter and
  resumption master secrets included. The trace's section 5 second ClientHello, as I first
  copied it, carried 4 extra zero bytes from a page break. Its header says 512 bytes, which
  is what the client sends.
- ChaCha20-Poly1305 (BL-673) is not used by the handshake itself: the handshake hands
  secrets to the record layer (BL-700) or QUIC, which run the AEAD.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line,
  100% branch, 313 members, 0 failing, worst CRAP 10. Curl.Tls.UnitTests: 269 tests.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Tls13ClientHandshake runs the full TLS 1.3 client handshake (HRR, all suites, X25519/NIST/FFDHE shares, RSA-PSS/ECDSA/Ed25519 CertificateVerify, client certificates); RFC 8448 sections 3 and 5 replay byte for byte
