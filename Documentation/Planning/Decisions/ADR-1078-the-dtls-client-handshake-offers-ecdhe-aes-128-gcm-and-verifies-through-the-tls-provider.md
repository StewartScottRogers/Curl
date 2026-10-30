# ADR-1078 — The DTLS client handshake offers ECDHE with AES-128-GCM and verifies through the TLS provider

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4251.

## Context

The CoAP extension's `coaps://` (BL-4252) needs a DTLS 1.2 client (RFC 6347). `Curl.Tls`'s `Tls12ClientHandshake`
reads TLS records from a stream and its ClientHello has no cookie, so it cannot take DTLS framing; the lane rules
forbid changing `Curl.Tls` for an extension. Upstream curl has no CoAP, so no curl behaviour decides the details.

## Decision

- `Curl.Extension.Coap.Dtls.Handshake.DtlsClientHandshake` is its own state machine over `IDatagramChannel`, built
  on `Curl.Tls`'s public pieces (`ServerHello`, `Tls12CertificateMessage`, `Tls12ServerKeyExchange`,
  `Tls12ClientKeyExchange`, `EcdhKeyShare`, `TlsPrf`) and the extension's own `Dtls/Wire`, `Dtls/Record` and
  `Dtls/Retransmission`.
- It offers TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256 and TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256 and (BL-4283) TLS_ECDHE_ECDSA_WITH_AES_128_CCM_8 (RFC 7251), the suites
  the record layer (`Dtls/Record`, BL-4266) protects. RFC 7252 section 9.1.3.2 makes
  TLS_ECDHE_ECDSA_WITH_AES_128_CCM_8 the CoAP baseline; BL-4283 adds it on the BCL's `AesCcm`. Group secp256r1;
  signature schemes ecdsa_secp256r1_sha256, rsa_pss_rsae_sha256 and rsa_pkcs1_sha256; server_name for a host name.
- The chain goes to `ITlsProvider.VerifyServerCertificateChain` (ADR-1052), so `--cacert`, `--capath`, `-k` and
  `--pinnedpubkey` apply as for curl's TLS; a rejection ends with the verdict's exit code (60 for an untrusted
  chain). The ServerKeyExchange signature is checked against the leaf even under `-k`, as OpenSSL does.
- Every other failure, the retransmission limit (ADR-1061) included, is exit 35 (`CURLE_SSL_CONNECT_ERROR`).
- It offers extended_master_secret (RFC 7627, BL-4282). When the ServerHello echoes it, the master secret comes from
  `TlsPrf.Sha256.ComputeExtendedMasterSecret` over the SHA-256 of the transcript through ClientKeyExchange; when it
  does not, from the classic client and server randoms. The test server echoes it when `EchoExtendedMasterSecret` is set.
- A flight is resent byte for byte, record sequence numbers included; the peer's replay window drops a repeated
  protected Finished and answers the repeated ClientKeyExchange with its own last flight.

## Consequences

A `coaps://` server that offers only PSK suites fails with exit 35 until a task adds them.
