# ADR-1431: Only a SHA-1 CertID names a certificate in an OCSP response

- Status: Accepted
- Date: 2026-11-26
- Decided by Claude under Stewart's delegation (BL-6027; fitness finding STD-230)

## Context

`OcspStapleVerifier` matched a `SingleResponse` to the leaf under whatever hash its `CertID`
named (SHA-1, SHA-256 and the rest of the SHA-2 family). Curl asks by SHA-1. The fitness
review measured curl 8.22.0 Schannel below a non-self-signed `--cacert` anchor: when the
responder answers with a SHA-256 `CertID`, good or revoked, it reports "the revocation status
is unknown", and `--ssl-revoke-best-effort` accepts the revoked leaf. Curl accepted the good
answer and refused the revoked one even under best effort.

## Decision

A `SingleResponse` names the leaf only when its `CertID` is hashed with SHA-1. Any other is
`CertificateNotFound`.

The one verifier serves the OCSP check below the anchor, the Schannel build's stapled status
and `--cert-status`, so the rule holds for all three. curl's OpenSSL build builds its own
`CertID` with SHA-1 and looks it up with `OCSP_resp_find_status`, whose comparison includes
the hash algorithm, so another hash is not found there either. That is read from curl's and
OpenSSL's behaviour as documented, not measured.

## Consequences

- Below the anchor, such an answer is no answer: the CRL decides, and with no distribution
  point the status is unknown.
- A staple with such a `CertID` leaves the Schannel build's online check to decide.
- Under `--cert-status` it fails as a response that does not name the certificate.
