# ADR-1244 — A Schannel --ciphers algorithm list filters the offered suites

- Status: Accepted
- Date: 2026-11-11
- Task: BL-4911
- Decided by Claude under Stewart's delegation.

## Context

curl's Schannel build hands a `--ciphers` ALG_ID list to the legacy credential's
`palgSupportedAlgs`. Curl validated the list (ADR-0896) and capped the range at TLS 1.2, but
offered every suite. The fitness review (STD-008) measured curl 8.22.0 Schannel on Windows 11:
`CALG_AES_128` and `CALG_AES_128:CALG_SHA_256` negotiate `ECDHE-RSA-AES128-GCM-SHA256`, and
`CALG_RSA_KEYX` against an ECDHE-only server is exit 35 with `SEC_E_ILLEGAL_MESSAGE`'s fatal-alert line.
`SslStream` cannot restrict suites on Windows (`CipherSuitesPolicy` is not supported there).

## Decision

- In the Schannel build a `--ciphers` list routes the connection to the hand-built client
  (`TlsClientRouting`, a build-specific row).
- `SchannelAlgorithmList.OfferedSuites` filters the Schannel profile's suites class by class
  (`GET_ALG_CLASS`: key exchange, signature, bulk cipher, hash): for each class the list names an
  accepted algorithm of, a suite's algorithm of that class must be named; other classes are not
  restricted. This matches all three measurements. An AEAD suite's hash is its PRF's (SHA-256 or
  SHA-384); an algorithm with no ALG_ID (Camellia, ARIA, ChaCha20) is never allowed in a
  restricted class. TLS 1.3 suites are left alone (the list caps the range at TLS 1.2 anyway).
- A list that leaves no suite is exit 35 with Schannel's algorithm-mismatch text, as a list
  Schannel accepts none of already is.

## Consequences

A received fatal alert is already the Schannel build's `SEC_E_ILLEGAL_MESSAGE` line on the
hand-built path, so `CALG_RSA_KEYX` against a server without RSA key exchange gives REF's exit 35.
