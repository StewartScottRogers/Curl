# ADR-1423: An expired delegated OCSP responder is not authorised

- Status: Superseded in part by [ADR-1438](ADR-1438-cert-status-refuses-a-not-yet-valid-delegated-ocsp-responder.md) (a not-yet-valid responder under `--cert-status`)
- Date: 2026-11-26
- Decided by Claude under Stewart's delegation (BL-6008; fitness finding STD-215)

## Context

`OcspStapleVerifier` checked a delegated responder's extended key usage, issuer and signature,
but not its validity period. Below a non-self-signed `--cacert` anchor, Curl on Windows therefore
accepted "good" from an expired responder, even for a certificate the CA's own CRL revokes.
The fitness review measured curl 8.22.0 Schannel: it treats such an answer as no answer, so the
CRL decides ("has been revoked", or "the revocation status is unknown" when there is no list).
It accepts an answer from a responder that is not yet valid.

## Decision

A delegated responder whose `notAfter` is before the check time is `ResponderNotAuthorised`.
There is no leeway. A responder that is not yet valid stays authorised, as curl's Schannel
build accepts it. The issuer signing its own response is not checked for dates here; chain
verification judges the issuer.

The one verifier serves the OCSP check below the anchor, the Schannel build's stapled status
and `--cert-status`, so all three refuse an expired responder. For `--cert-status` that gives
curl's "OCSP response verification failed".

## Consequences

- Below the anchor, an expired responder's answer falls back to the CRL.
- curl's OpenSSL build is expected to refuse a not-yet-valid responder under `--cert-status`
  too. That is not yet measured, and BL-6037 covers it.
