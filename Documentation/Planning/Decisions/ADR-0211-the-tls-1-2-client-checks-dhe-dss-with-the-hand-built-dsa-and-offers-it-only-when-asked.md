# ADR-0211 — The TLS 1.2 client checks DHE_DSS with the hand-built DSA and offers it only when asked

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-802.
Builds on ADR-0140 (DHE-DSS and DSA are in what the client supports), ADR-0118 (DSA is
hand-built in `Curl.Cryptography`) and ADR-0154 (the TLS 1.2 client's alerts).

## Context

`Tls12ClientHandshake` ran ECDHE, DHE, RSA and anonymous suites. OpenSSL 3.5.5's
`ALL:COMPLEMENTOFALL` also holds 13 `DHE_DSS` suites, whose ServerKeyExchange is signed by
a DSA certificate: in TLS 1.2 under one of `dsa_sha1`, `dsa_sha224`, `dsa_sha256`,
`dsa_sha384` and `dsa_sha512` (RFC 5246 section 7.4.1.4.1), in TLS 1.0 and 1.1 over
SHA-1 alone (RFC 2246 section 7.4.3), always as the DER `Dss-Sig-Value` of r and s. The
BCL's `DSA` cannot be relied on for this on macOS, and the BCL does not hash SHA-224.

## Decision

- **Suites.** `Tls12Authentication.Dss` and the 13 `DHE_DSS` suites join
  `Tls12CipherSuite`; a `Dss` suite takes a certificate whose key is `id-dsa`
  (1.2.840.10040.4.1) and nothing else (`handshake_failure` otherwise, as for RSA and ECDSA).
- **Verification** is `Curl.Cryptography`'s `DsaSignature.VerifyHash`, over the hash
  `DsaSignature.HashData` computes. `HashData` is new public surface in
  `Curl.Cryptography` because SHA-224 lives there and nowhere in the BCL; putting it beside
  the DSA it serves avoids a second SHA-224 in `Curl.Tls`.
- **Alerts** follow ADR-0154's split: a DSA key whose `Dss-Parms` or public INTEGER does
  not read (not DER, a trailing value, a value of 0 or below) is `bad_certificate`, as an
  RSA key that does not import is; a `Dss-Sig-Value` that does not decode (not DER, a
  trailing byte or value, a negative r or s, one longer than q) or does not verify is
  `decrypt_error`, as OpenSSL's `DSA_verify` failure is.
- **Defaults stay as they are.** `Tls12ClientSettings`' default suites and signature
  schemes do not gain DSS or `dsa_*`: none of the three measured default ClientHellos
  (ADR-0140) offers a DSS suite (OpenSSL 3.5.5's does list `dsa_*` in
  `signature_algorithms`; `ClientHelloProfile.OpenSsl` carries that). A caller that wants
  DSS sets `CipherSuites` and `SignatureAlgorithms`; routing `--ciphers DHE-DSS-*` there
  is BL-891's, and `--sigalgs` BL-709's.
- **A key whose domain parameters DSA refuses** (q not 160, 224 or 256 bits, p longer
  than 10,000 bits, g out of range) reads, but `VerifyHash` answers false for it, so its
  signature is `decrypt_error`; judging a key's size is the certificate verifier's part.
- **The client does not sign with DSA.** A DSA client certificate's CertificateVerify is
  not part of this decision; no `TlsSigningKey` signs DSA yet.

## Consequences

- `Tls12ClientHandshake` completes a DHE_DSS handshake at TLS 1.2, 1.1 and 1.0 when the
  caller offers the suite (and, at TLS 1.2, a `dsa_*` scheme); `curl --ciphers` reaches it
  once BL-891 lands.
- A DSA client certificate cannot be presented until a DSA `TlsSigningKey` exists.
