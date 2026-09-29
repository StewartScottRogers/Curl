# ADR-0173 — The hand-built TLS client checks a stapled OCSP response in OpenSSL's order and fails the handshake with a typed outcome

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-705.

## Context

`--cert-status` asks the server to staple an OCSP response and fails the transfer with
exit 91 `CURLE_SSL_INVALIDCERTSTATUS` unless the response vouches for the server's
certificate. `SslStream` exposes no stapled response, so ADR-0140 routes `--cert-status`
through the hand-built client in `Curl.Tls.UnitLibrary` and leaves the chain to the one
shared `IServerCertificateVerifier`, with the stapled response checked by the client
itself (`OcspStapleVerifier`, "The verification hand-off"). The Schannel build accepts
the option but checks nothing it can report; curl's OpenSSL build checks the response
in `verifystatus()` (lib/vtls/openssl.c). Both handshakes already sent `status_request`
and handed the stapled bytes to the verifier (BL-699, BL-703); nothing judged them.

Open questions: what the check covers and in which order, how its outcome reaches the
caller, what the client sends when it fails, where the issuer and responder come from,
and what a resumed TLS 1.2 session does.

## Decision

1. **`OcspStapleVerifier.Verify(response, chain, now)`** returns an `OcspStapleOutcome`:
   an `OcspStapleStatus` and a `Code` (the CRL reason of a revoked certificate, -1 when
   none was given, or the `responseStatus` of an unsuccessful response). It checks in
   the order curl's OpenSSL build does, so the first failure it reports is the one curl
   would:
   `NoResponse` (none stapled, or empty) → `Malformed` (not a DER `OCSPResponse`
   carrying a `BasicOCSPResponse`) → `Unsuccessful` → `IssuerNotFound` →
   `ResponderNotAuthorised` (no certificate matches the `ResponderID`) →
   `SignatureInvalid` → `ResponderNotAuthorised` (neither the issuer nor a certificate
   the issuer signed with `id-kp-OCSPSigning`, RFC 6960 section 4.2.2.2) →
   `CertificateNotFound` (no `SingleResponse` names the leaf by `CertID`) → `Revoked` /
   `Unknown` → `Expired`. A revoked certificate is reported before a stale response, as
   curl does. `Good` is the only pass.
2. **Time** comes from `TimeProvider` on `Tls13ClientSettings` and `Tls12ClientSettings`
   (default `TimeProvider.System`). The window is OpenSSL's `OCSP_check_validity` with
   curl's arguments: `thisUpdate` no more than five minutes ahead, `nextUpdate` (when
   present) no more than five minutes past and not before `thisUpdate`, no maximum age.
3. **Issuer and responder** come from what the server sent: the issuer is the certificate
   in the presented chain whose subject is the leaf's issuer (a self-signed leaf is its
   own); a delegated responder must be in the response's `certs` and be signed directly
   by that issuer. The chain itself, and so the issuer's trust, is the verifier's
   (ADR-0140). `CertID` hashes may be SHA-1, SHA-256, SHA-384 or SHA-512; response
   signatures RSA PKCS #1 v1.5 and ECDSA with the same four hashes, RSASSA-PSS with its
   hash from the parameters (SHA-1 when absent), and Ed25519. Anything else is
   `SignatureInvalid` or `CertificateNotFound`, never an exception.
4. **The handshake fails.** With `RequestOcspStatus`, each handshake runs the check once
   the verifier has accepted the chain (TLS 1.3 at the Certificate, TLS 1.2 once any
   CertificateStatus has had its chance to arrive), exposes the outcome as
   `CertificateStatus`, and on anything but `Good` sends `bad_certificate_status_response`
   (113, RFC 8446 section 6.2) and returns a `TlsHandshakeFailure` whose new
   `CertificateStatusRejection` holds the outcome. curl's OpenSSL build finishes the
   handshake and then closes; failing inside it sends the alert RFC 6066 section 8
   provides instead, and no request is ever written, while the exit and message the
   user sees - BL-610 maps the outcome to exit 91 and curl's text - are the same. A
   chain the verifier rejects is exit 60 and is never status-checked.
5. **The request.** TLS 1.3 gains `RequestOcspStatus` (TLS 1.2 had it): the client builds
   `status_request` with no responder IDs and no extensions, as OpenSSL sends, right
   after `supported_groups` in `DefaultExtensionOrder` (OpenSSL's position); a settings
   record that asks for OCSP status with no `status_request` in its order is refused.
   Without `RequestOcspStatus` a stapled response still goes to the verifier unchecked.
6. **A resumed TLS 1.2 session** presents no certificate and is not checked;
   `CertificateStatus` stays `null`. Whether `--cert-status` resumes sessions at all is
   BL-610's to decide with the rest of the wiring.

## Consequences

- `--cert-status` has a typed outcome per failure curl tells apart, pinned in
  `Curl.Tls.UnitTests` (`OcspStapleVerifierTests`, and `OcspStaplingHandshakeTests` over
  TLS 1.3 and TLS 1.2 against the in-memory servers, which now send an issuer chain).
  BL-610 turns each into curl's measured message and exit 91.
- Responses are parsed with `System.Formats.Asn1` and checked with the BCL's `RSA` and
  `ECDsa` and `Curl.Cryptography`'s Ed25519 through `TlsCertificatePublicKey`; no package.
- A delegated responder is trusted by its issuer's signature alone, not by building its
  own chain to a trust anchor as `OCSP_basic_verify` can; for the RFC 6960 profile - a
  responder the issuer certified directly - the answer is the same.
