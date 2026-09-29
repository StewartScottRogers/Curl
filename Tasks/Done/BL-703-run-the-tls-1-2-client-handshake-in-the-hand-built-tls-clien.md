---
id: BL-703
title: Run the TLS 1.2 client handshake in the hand-built TLS client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-702, BL-698, BL-671]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Documentation/Planning/Decisions/ADR-0152-the-tls-1-2-client-handshake-refuses-legacy-renegotiation-ignores-hello-request-and-matches-openssls-alerts.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-703 — Run the TLS 1.2 client handshake in the hand-built TLS client

## Goal

The hand-built client runs a TLS 1.2 (and 1.1/1.0 when the version range allows) client handshake: ECDHE (X25519 and NIST curves) and RSA key exchange, the suites BL-695's ADR lists, `renegotiation_info` (RFC 5746), extended master secret, ALPN, SNI, `status_request`, session-ID and ticket resumption (RFC 5077), an optional client certificate, and the chain handed to the verifier.

## Context

- Design: BL-695's ADR. Builds on BL-702 (records and PRF), BL-698 (the shared ClientHello encoder and extension codecs) and X25519 (BL-671).
- References: RFC 5246 section 7, RFC 8422 (ECDHE and point formats), RFC 5746, RFC 5077, RFC 6066.

## Acceptance criteria

- [x] An in-memory TLS 1.2 server in `Curl.Tls.UnitTests` completes handshakes for each suite family and key exchange, resumes by session ID and by ticket, and the client rejects a bad ServerKeyExchange signature, a bad Finished and a missing `renegotiation_info` with the typed alert; a TLS 1.0 and a 1.1 handshake complete when the range allows them and fail with `protocol_version` otherwise.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Plan: `Tls12ClientHandshake` mirrors the I/O-free `Tls13ClientHandshake`, with the
  ChangeCipherSpec as its own input (`ReceiveChangeCipherSpec`) and output message, and
  `RecordProtection` / `KeyBlock` handed to the caller for BL-702's record states. The
  server's full flight is checked as an ordered list of steps (Certificate,
  CertificateStatus, ServerKeyExchange, CertificateRequest, ServerHelloDone), each
  permitted and required by the suite and the ServerHello (`Tls12ServerFlightStep`).
- Decisions in ADR-0152 (decided by Claude under Stewart's delegation): refuse a server
  without `renegotiation_info` with `handshake_failure` as OpenSSL does; ignore
  HelloRequest; refuse DHE under 1024 bits with `handshake_failure`; offer a ticket with
  a random session ID; hand the chain to the verifier after any CertificateStatus; take
  each alert from OpenSSL's client (a key of the wrong type for the suite is
  `handshake_failure`, per `ssl3_check_cert_and_algorithm`).
- `touches` gained the ADR file and the ADR index `Documentation/Planning/Decisions/README.md`,
  which the task needs for its decision record; no task in Doing names either
  (BL-512 touches FTP and Curl.Console, BL-676 Curl.Cryptography).
- Signatures: TLS 1.2 adds `rsa_pkcs1_*` and `ecdsa_sha1` and binds `ecdsa_*` to no curve
  (a separate rule table, so TLS 1.3's CertificateVerify rules are unchanged). TLS 1.0/1.1
  RSA signatures are checked with `BigInteger` s^e mod n (public, no secret); an RSA
  client key cannot sign them yet (the BCL has no DigestInfo-free PKCS #1 signing, and a
  `BigInteger` private operation would leak timing), so it answers with an empty
  Certificate. ECDSA client keys sign SHA-1 there.
- Suite table: 73 ECDHE_ECDSA, ECDHE_RSA, DHE_RSA, RSA, DH_anon and ECDH_anon suites over
  AES-CBC/GCM, ChaCha20-Poly1305, Camellia-CBC, ARIA-GCM, 3DES and NULL. Default offer is
  the 27 ECDHE/DHE/RSA AEAD and CBC suites, no anonymous or NULL ones.
- Default taken: the ClientHello follows OpenSSL's extension order (renegotiation_info,
  server_name, ec_point_formats, supported_groups, session_ticket, status_request, ALPN,
  encrypt_then_mac, extended_master_secret, signature_algorithms), with no caller-set
  order; a caller-set order like TLS 1.3's can come with the ClientHello profiles.
- Tests: `Tls12TestServer` (in-memory, resumes from a shared `Tls12TestSessionCache`),
  `Tls12HandshakeDriver`; 567 tests in Curl.Tls.UnitTests pass.
- Review (code-reviewer) fixes: an all-zero DHE shared secret (a composite p = q^2 with
  Ys = q) is now `illegal_parameter` rather than an exception; TLS 1.0/1.1 RSA keys are
  read straight from the DER `RSAPublicKey` and held to OpenSSL's limits (modulus of 47
  bytes to 16384 bits, exponent of 1 to 64 bits), a malformed one `bad_certificate`; the
  DHE minimum counts bits, not bytes; a non-empty `extended_master_secret`,
  `encrypt_then_mac`, `session_ticket` or `status_request` echo is `decode_error`;
  `Tls12Session` documents that sessions are kept per host and port. Considered and left:
  the CertificateRequest's `certificate_types` is not matched against the client key
  (the server's `supported_signature_algorithms` already gates TLS 1.2).
- Conformance stage: no CLI surface changes (the handshake is not wired to an option
  until BL-708), so there is no curl output to compare.
- Follow-ups filed by task-planner: BL-797 (CCM/CCM8/RC4 suites, after BL-796), BL-798
  (DHE-DSS suites, after BL-745), BL-799 (x448 and brainpool ECDHE, after BL-740 and
  BL-742), BL-800 (RSA signing of a TLS 1.0/1.1 CertificateVerify).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Tls12ClientHandshake runs TLS 1.2/1.1/1.0 handshakes (ECDHE, DHE, RSA, anonymous; 73 suites), resumes by session ID and ticket, and refuses bad signatures, Finished and missing renegotiation_info with typed alerts
