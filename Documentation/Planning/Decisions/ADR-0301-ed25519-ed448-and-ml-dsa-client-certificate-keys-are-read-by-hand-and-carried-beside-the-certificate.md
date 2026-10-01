# ADR-0301 — Ed25519, Ed448 and ML-DSA client certificate keys are read by hand and carried beside the certificate

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1066.

## Context

OpenSSL-built curl presents a `--cert` whose key is Ed25519, Ed448 or ML-DSA-44/65/87 and signs
the CertificateVerify with it. `ClientCertificateLoader` loaded every `--key` through
`X509Certificate2.CreateFromPem`, which holds no Ed25519 or Ed448 key on any platform and an
ML-DSA key only where the BCL's `MLDsa` is supported (ADR-0118), so such a certificate either
failed to load or reached `HandBuiltTlsProvider.ToTlsClientCertificate` with no key it could
use. `Curl.Tls.UnitLibrary` already has the hand-built signing keys (`Ed25519TlsSigningKey`,
`Ed448TlsSigningKey`, `MlDsaTlsSigningKey`, BL-1064).

## Decision

1. The OpenSSL build's PEM/DER loading decides by the certificate's public key algorithm, not
   by trying the BCL first: for `1.3.101.112` (Ed25519), `1.3.101.113` (Ed448) and
   `2.16.840.1.101.3.4.3.17/.18/.19` (ML-DSA) the key is read by
   `HandBuiltPrivateKeyReader` on every platform, so the result never depends on whether the
   platform's BCL happens to support ML-DSA; every other algorithm goes to the BCL as before.
2. The key is a PKCS #8 `PrivateKeyInfo` read with `System.Formats.Asn1`: from a DER `--key`
   (`--key-type DER`) as it is, from a PEM file as its first `PRIVATE KEY` block. Ed25519 and
   Ed448 take RFC 8410's `CurvePrivateKey`; ML-DSA takes any of RFC 9881's `seed` [0],
   `expandedKey` and `both` forms, and `both` must hold the expanded key its seed generates.
   Algorithm identifier parameters, trailing data, a key of another algorithm than the
   certificate's, and a key whose public half is not the certificate's are refused, as
   OpenSSL's `SSL_CTX_use_PrivateKey` refuses a mismatched key.
3. A refused or unreadable key is the failure an unusable `--key` already gives: exit 43,
   `unable to set private key file: '<key>' type <type>` (measured 2026-09-26 against curl
   8.18.0 with OpenSSL 3.5.5). The task text's "exit 58" was a slip; matching today's
   measured behaviour wins.
4. The signing key travels beside the certificate as a `HandBuiltKeyCertificate`, an
   `X509Certificate2` copy with a `SigningKey` property, so no loader signature or caller
   changes; `ToTlsClientCertificate` takes its key first, before the BCL's RSA and ECDSA keys.
   The copy has no BCL private key, so the `SslStream` provider cannot present it; only the
   hand-built client can, which is the client that speaks these schemes.
5. An `ENCRYPTED PRIVATE KEY` for these algorithms is not decrypted yet (exit 43); BL-1085
   does that.

## Consequences

- `curl --cert ed25519.pem --key ed25519.key https://...` presents the certificate in the
  hand-built TLS client on every platform, as the OpenSSL build does.
- A PKCS #12 `--cert` with one of these keys still goes to the BCL's PKCS #12 loader.
