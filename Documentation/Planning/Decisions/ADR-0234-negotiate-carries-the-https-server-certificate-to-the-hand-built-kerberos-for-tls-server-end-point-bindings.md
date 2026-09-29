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
