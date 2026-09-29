# ADR-0234 — Negotiate carries the HTTPS server certificate to the hand-built Kerberos for tls-server-end-point bindings

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-915.
Builds on ADR-0171 (the hand-built GSS-API initiator), whose `KerberosGssContextOptions.ChannelBindings`
BL-832 added.

## Context

BL-832 measured curl 8.18.0 with MIT krb5 1.22.1 over HTTPS: the authenticator checksum's
`Bnd` is the MD5 of RFC 2744's channel bindings structure with no addresses and the
application data `tls-server-end-point:` followed by the SHA-256 of the sha256RSA server
certificate (RFC 5929 section 4.1). The hand-built Kerberos could take that application data
but nothing gave it the certificate: `SecurityContextRequest` and `HttpAuthRequest` carried
none.

## Decision

- `HttpAuthRequest.ServerCertificate` and `SecurityContextRequest.ServerCertificate`
  (`Curl.Protocol.Abstractions`) carry the TLS server certificate's DER as a
  `ReadOnlyMemory<byte>`, the form `ConnectResult.PeerCertificates` already uses; empty, the
  default, means no TLS. `NegotiateHttpAuthenticator` copies one to the other.
- `TlsServerEndPointChannelBindings.Of` (`Curl.Authentication`) makes the application data:
  the certificate's hash in its signature algorithm's hash, SHA-256 for MD5 and SHA-1, for
  the RSA PKCS #1, ECDSA and DSA signature OIDs with MD5, SHA-1, SHA-256, SHA-384 and SHA-512.
  `HandBuiltKerberosSecurityContext` passes it as `KerberosGssContextOptions.ChannelBindings`.
- No certificate gives `null`, so zeros, as curl sends over plain HTTP.
- A signature algorithm outside that table (RSASSA-PSS, Ed25519, Ed448, SHA-224) gives
  `null` too, provisionally. curl's OpenSSL build looks the digest up from the signature and,
  as read from source, fails when there is none; that is not yet measured, and BL-965 measures
  and matches it.
- The system GSS-API and SSPI routes are unchanged: the BCL's `NegotiateAuthentication`
  takes its own `ChannelBinding`, and curl's SSPI build is matched separately.
- Filling `HttpAuthRequest.ServerCertificate` from the connection is the HTTP handler's
  (BL-966); until then every request's is empty.

## Consequences

- The contract grows by one optional member on each record; every existing caller keeps
  compiling and sends no bindings, as before.
- Record equality on the two records compares the certificate by reference, which no caller
  relies on.

## Amendment (BL-965): a signature that names no hash fails the transfer

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-965. This
replaces the provisional "no bindings" for a signature algorithm outside the table.

**Measured** 2026-09-29 with WSL Ubuntu's curl 8.18.0 (OpenSSL 3.5.5, `mit-krb5/1.22.1`), the
only Linux curl at hand: `openssl s_server -HTTP` served a 401 `WWW-Authenticate: Negotiate`
with a throwaway certificate of each kind to
`curl -v -k --negotiate -u : https://server.example.test:18965/neg`, with a ticket from the
user-space MIT KDC and again with no credential cache. (`Record-CurlExchange.ps1 -Tls` serves
through Schannel, which cannot serve an Ed25519 or Ed448 certificate, so OpenSSL's own server
stood in; nothing was written.)

| Server certificate's signature | curl 8.18.0 |
| --- | --- |
| RSASSA-PSS (SHA-256) | No request sent. `-v`: `* Could not find digest algorithm UNDEF (NID 0)` after `* using HTTP/1.x`; stderr `curl: (91) Could not find digest algorithm UNDEF (NID 0)`; exit 91. The same with no ticket. |
| Ed25519 | The same, exit 91. |
| Ed448 | The same, exit 91. |
| sha224WithRSAEncryption | The Negotiate token is sent; the transfer ends on the 401 with exit 0. |
| sha256WithRSAEncryption | The Negotiate token is sent, as BL-832 measured. |

This is curl's `ossl_get_channel_binding`: OpenSSL pairs PSS, Ed25519 and Ed448 with
`NID_undef` for the digest, `EVP_get_digestbynid` finds none and curl fails with
`CURLE_SSL_INVALIDCERTSTATUS`; `spnego_gssapi.c` asks for the bindings before
`gss_init_sec_context`, so before any credential is looked at.

**Decision.**

- `TlsServerEndPointChannelBindings.Of` reads the outer `signatureAlgorithm` OID from the DER
  (`System.Formats.Asn1`, so the same on every platform), and for a signature outside the table
  throws `HttpAuthenticationFailedException` with exit 91 (`SslInvalidCertStatus`): the
  measured `Could not find digest algorithm UNDEF (NID 0)` for RSASSA-PSS, Ed25519 and Ed448,
  and, for an OID OpenSSL does not know at all, `Unable to find digest NID for certificate
  signature algorithm`, read from curl's source (OpenSSL's server refuses such a certificate,
  so it could not be measured). The HTTP handler already fails the exchange with that code
  and message once connected.
- `HandBuiltKerberosSecurityContext` makes the bindings before it asks for a ticket, so the
  failure comes even without one, as measured.
- SHA-224 signatures (RSA 1.2.840.113549.1.1.14, ECDSA 1.2.840.10045.4.3.1, DSA
  2.16.840.1.101.3.4.3.1) take SHA-224, hand-built as `Sha224` in `Curl.Authentication` since
  the BCL has none; tests pin it to NIST's examples and to OpenSSL's hash of the measured
  certificate.
- Other signatures OpenSSL pairs with a digest the table lacks (the SHA-3 family) still fall
  to the unknown-OID failure until BL-980 measures and matches them.
