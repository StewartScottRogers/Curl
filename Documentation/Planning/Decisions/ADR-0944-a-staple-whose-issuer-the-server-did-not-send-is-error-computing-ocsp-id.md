# ADR-0944: A staple whose issuer the server did not send is `Error computing OCSP ID`

- **Status:** Accepted
- **Date:** 2026-10-28

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-3917.

## Context

With `--cert-status` and a trust store (`--cacert`, no `-k`), the OpenSSL build of Curl refused a
stapled response from a server that sends only its leaf with exit 91 `Error finding issuer
certificate` (`OcspStapleStatus.IssuerNotFound`). Measured with curl 8.18.0 on OpenSSL 3.5.5 (Ubuntu,
WSL) against `openssl s_server -WWW -status_file` with no `-cert_chain` and a good staple signed by the
root: curl exits 91 `curl: (91) Error computing OCSP ID`, the text `verifystatus()` prints when it has no
issuer to build the leaf's `CertID` from. Under `-k` both print `OCSP response verification failed`
(ADR-0862).

## Decision

`CertificateStatusFailureMessages` maps `IssuerNotFound` to `Error computing OCSP ID`. The outcome and
`OcspStapleVerifier`'s order (ADR-0173) are unchanged; only the text is curl's.

## Consequences

- The same repro through Curl's linux-x64 build now gives curl's exit code and text, with and
  without `-k`.
- The Schannel build ignores `--cert-status` (ADR-0795), so only the OpenSSL build prints it.
