# ADR-0862: `--cert-status` under `-k` trusts no OCSP responder

- **Status:** Accepted
- **Date:** 2026-10-26

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-3696 (CGR-167).

## Context

Under `-k` with `--cert-status` the OpenSSL build of Curl judged the stapled OCSP response as it does
with a trust store: a good staple completed the transfer, a revoked one failed with its revocation
reason and one for another certificate with `Could not find certificate ID in OCSP response`.
Measured with curl 8.18.0 on OpenSSL 3.5.5 (Ubuntu, WSL) against `openssl s_server -status_file`:
under `-k`, `--cacert` given or not, every one of those, and a staple whose issuer the server did not
send, is exit 91 `OCSP response verification failed`. curl loads no trust store when it does not
verify the peer, so `verifystatus()`'s `OCSP_basic_verify` cannot build the responder's chain to any
anchor. No staple stays `No OCSP response received`; `verifystatus()` refuses an unreadable or
unsuccessful response before `OCSP_basic_verify`, so those keep their own text too.

## Decision

`HandBuiltTlsProvider` hands its `HandBuiltCertificateVerifier` its clock (`untrustedStapleClock`)
when `-k` and `--cert-status` are both set. Once the certificate is otherwise accepted, the
verifier reads the staple with `OcspStapleVerifier`, and every outcome other than no response, a
malformed one or an unsuccessful one is refused as exit 91 with
`CertificateStatusFailureMessages`' `SignatureInvalid` text and a `bad_certificate_status_response`
alert, as the status check's own refusals are sent. A refused certificate or pin keeps its own
failure. The Schannel build ignores `--cert-status` (ADR-0795), so only the OpenSSL build reaches it.

## Consequences

- The verifier refuses before `Curl.Tls`' status check runs, so `-v` prints no `SSL certificate
  status:` line under `-k`, as curl stops before it.
- `Curl.Tls` is unchanged; the refusal lives in the Networking verifier, beside the other choices
  `-k` makes.
