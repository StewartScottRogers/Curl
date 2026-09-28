# ADR-0103 — The Schannel build matches the common name of a `--cacert` certificate without DNS names

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-415.

## Context

BL-150 measured curl 8.21.0's Schannel build with `--cacert` against a certificate whose
subjectAltName holds only `127.0.0.1` and whose CN is `localhost`: `https://localhost`
succeeds. With `--cacert` curl's Schannel build checks the name itself
(`schannel_verify.c`): `CertGetNameString(CERT_NAME_DNS_TYPE)` returns the DNS names of the
subjectAltName, or the CN when it holds none, and each is matched with
`Curl_cert_hostcheck`. .NET's own name check does not give the same answer on every
platform: it accepts the certificate on Windows and can report
`SslPolicyErrors.RemoteCertificateNameMismatch` elsewhere.

## Decision

- **With `--cacert`, the Schannel build drops a name mismatch when the CN matches**:
  `SslStreamTlsProvider` asks `SchannelCommonNameCheck.CommonNameMatches`, which is true
  only when the target is a host name (not an IP literal), the certificate has no DNS
  subjectAltName, and its CN matches the target.
- **The CN is matched as `Curl_cert_hostcheck` matches**: case ignored, one trailing dot
  ignored on either side, and a pattern starting `*.` with at least one more dot stands
  for exactly one left-most label of a host that is not an IP literal (`*.example.com`
  matches `www.example.com`, not `a.b.example.com` or `example.com`; `*.com` matches
  nothing; `w*.example.com` is compared literally).
- **Without `--cacert` nothing changes**: Schannel's own check stands and a mismatch is
  still `SEC_E_WRONG_PRINCIPAL`.
- **The OpenSSL build is unchanged**: it reports whatever mismatch .NET found.
- **A certificate with DNS subjectAltNames is left to .NET's check**, which matches those
  names; its wildcard rules are not replaced here.

## Consequences

- The Schannel build's answer for this certificate is the same on every platform, not
  just where .NET happens to fall back to the CN.
- The OpenSSL build still follows .NET for a CN-only name. On Windows .NET accepts the
  certificate where curl's OpenSSL build would refuse it (it falls back to the CN only
  without DNS or IP subjectAltNames); that build runs on Windows only in tests.
