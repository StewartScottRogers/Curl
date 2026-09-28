---
id: BL-695
title: Decide how the hand-built TLS client in Curl.Tls.UnitLibrary is built and when Curl uses it
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-669]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-695 — Decide how the hand-built TLS client in Curl.Tls.UnitLibrary is built and when Curl uses it

## Goal

An ADR fixes how `Curl.Tls.UnitLibrary`, a hand-built TLS client, is structured (TLS 1.3 handshake usable without a record layer for QUIC; TLS 1.3 and 1.2/1.1/1.0 record layers for TCP), what it supports (versions, cipher suites, groups, signature algorithms, extensions including ALPN, SNI, `status_request`, session tickets, early data, ECH, SRP), how it hands the server's chain to Curl's existing certificate verification, and exactly when a transfer uses it instead of `SslStream`.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): nothing is left out because the BCL has no primitive for it. `SslStream` cannot run TLS inside QUIC (RFC 9001), and cannot control curves, signature algorithms, early data, ECH, session export, SRP, OCSP stapling or TLS 1.0/1.1 where the OS disables them; curl builds with OpenSSL, LibreSSL, wolfSSL or GnuTLS control all of these (curl.se's official Windows build of curl 8.22.0 uses LibreSSL 4.3.2 and ngtcp2, checked 2026-09-28 at https://curl.se/windows/).
- Rule of thumb for this ADR: `SslStream` stays the default wherever it can do what the command line asks, so output keeps matching the platform's curl (ADR-0009); the hand-built client is used for QUIC always, and over TCP only for what `SslStream` cannot do. Where both do the same thing, output text matches the platform's curl.
- Consumers: QUIC (BL-724), TLS options (BL-617 decides per option; BL-708 wires it), `--cert-status` (BL-610, BL-705), TLS 1.0/1.1 (BL-714), `--no-sessionid`/`--ssl-allow-beast` (BL-713).
- Primitives: BCL (`HKDF`, `AesGcm`, `ECDiffieHellman`, `RSA` PSS/PKCS#1, `ECDsa`, `X509Chain`) plus `Curl.Cryptography.UnitLibrary` (BL-669's ADR: X25519, Ed25519, ChaCha20-Poly1305, HPKE). Specifications: RFC 8446, RFC 8448 (traces), RFC 5246, RFC 7627, RFC 5746, RFC 6066, RFC 7301, RFC 9001 section 4, RFC 5054, the ECH specification (draft-ietf-tls-esni, or its RFC if published; cite what is current).
- Default ClientHello contents (suite and group order) should match what curl's official build sends: capture one ClientHello from curl.se's Windows build and one from a Linux OpenSSL build with `Record-CurlExchange.ps1 -NoServer` against a TCP listener, and record both in the ADR.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with the captured ClientHellos, the supported feature table, the class structure, the verification hand-off (no second verifier), and the routing rule between `SslStream` and the hand-built client.
- [ ] Consequences name BL-696 to BL-708 and what each relies on from the ADR, and state that no TLS feature any official curl build offers is refused.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
