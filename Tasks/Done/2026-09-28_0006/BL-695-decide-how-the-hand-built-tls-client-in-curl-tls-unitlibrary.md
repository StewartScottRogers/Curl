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
completed: 2026-09-28
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

- [x] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with the captured ClientHellos, the supported feature table, the class structure, the verification hand-off (no second verifier), and the routing rule between `SslStream` and the hand-built client.
- [x] Consequences name BL-696 to BL-708 and what each relies on from the ADR, and state that no TLS feature any official curl build offers is refused.
- [x] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

- ADR-0140 (0139 was the highest in the folder). Plan summary: `SslStream` stays the default; the hand-built client runs every QUIC handshake and a TCP one only when a routing row (`--curves`, `--sigalgs`, `--tls-earlydata`, `--ssl-sessions`, `--ech`, SRP, `--cert-status`, `--no-sessionid`, `--ssl-allow-beast`, `--tls-max` 1.0/1.1) holds; one verifier behind `IServerCertificateVerifier`, implemented in Networking by the code `SslStream`'s callback runs today.
- Captures: `Record-CurlExchange.ps1`'s plain server mode (not `-NoServer`, which sees no traffic) records the ClientHello in `request.bin` when the canned response is a TLS alert: the header read ends after its one-second wait. No script change was needed. Builds measured: WinGet curl 8.18.0 (curl.se official, LibreSSL 4.2.1, the installed version; 8.22.0 is current), mingw curl 8.21.0 Schannel, Ubuntu (WSL) curl 8.18.0 OpenSSL 3.5.5 through `-ListenAddress` and `--resolve`.
- Choice: the default ClientHello is the platform curl's measured profile (Schannel on Windows, OpenSSL elsewhere), LibreSSL's for QUIC on Windows, so a server chooses as it would for the curl being replaced.
- Choice: `--ssl-sessions` stores OpenSSL's `SSL_SESSION` DER as the opaque part, so session files interoperate with the OpenSSL build.
- ECH is RFC 9849 (published 2026-03, https://www.rfc-editor.org/info/rfc9849/).
- Filed: BL-783 (Camellia), BL-784 (ARIA and ARIA-GCM) — both amend ADR-0118's list and BL-702 now depends on them; BL-785 (decide where a hand-built Zstandard decoder lives), BL-786 (RFC 8879 certificate decompression, depends on BL-699 and BL-785).
- Not done here: the exact QUIC ClientHello is left to BL-724 (needs an Initial capture and Initial-key removal).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. ADR-0140 fixes the hand-built TLS client's structure, supported set, measured ClientHellos, single-verifier hand-off and the SslStream routing rule
