# ADR-1432: `--cert-status` refuses a not-yet-valid delegated OCSP responder; the Schannel revocation checks accept it

- Status: Accepted
- Date: 2026-11-26
- Decided by Claude under Stewart's delegation (BL-6037)
- Supersedes in part: [ADR-1423](ADR-1423-an-expired-delegated-ocsp-responder-is-not-authorised.md)
  (that a not-yet-valid responder is accepted under `--cert-status`)

## Context

ADR-1423 refused an expired delegated OCSP responder and kept a not-yet-valid one authorised
everywhere, as curl 8.22.0 Schannel accepts it below an anchor. Curl's `--cert-status`
follows curl's OpenSSL build (ADR-0191), whose answer for such a responder was not yet measured.

Measured on 2026-11-26 with curl 8.18.0 on OpenSSL 3.5.5 (WSL Ubuntu) against `openssl s_server
-status_file` on 127.0.0.1, TLS 1.3 and TLS 1.2, with a stapled "good" signed by a delegated
responder the leaf's issuer signed for `id-kp-OCSPSigning`:

| Responder's validity | Exit | `curl: (91)` text |
| --- | --- | --- |
| valid | 0 | none (`-v`: `SSL certificate status: good (0)`) |
| `notBefore` 2 days ahead | 91 | `OCSP response verification failed` |
| `notBefore` 120 seconds ahead | 91 | `OCSP response verification failed` |
| `notAfter` 120 seconds ago | 91 | `OCSP response verification failed` |

So OpenSSL's chain check of the responder gives no leeway at either end.

## Decision

`OcspStapleVerifier.Verify`, which both TLS handshakes run for `--cert-status` on every platform,
refuses a delegated responder whose `notBefore` is after the check time as
`ResponderNotAuthorised`, as it already refused one past its `notAfter`.

The Windows revocation checks (the server's stapled status and the OCSP query below a
non-self-signed anchor) call `OcspStapleVerifier.VerifyForSchannelRevocationCheck`, which differs
only in accepting a not-yet-valid responder.

## Consequences

- On Windows with `--cert-status`, a staple from a not-yet-valid responder passes the revocation
  check and then fails `--cert-status` with exit 91.
- `-k` with `--cert-status` is unchanged: it trusts no responder (ADR-0191).
