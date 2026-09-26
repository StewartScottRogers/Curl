---
id: BL-059
title: Decide which curl TLS build Curl matches for TLS failure messages, --capath and --cert formats
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed:
---
# BL-059 — Decide which curl TLS build Curl matches for TLS failure messages, --capath and --cert formats

## Goal

Stewart decides, per platform, which upstream curl TLS build Curl reproduces for three
things the BCL could do either way: the text of a TLS failure message, whether `--capath`
is honoured, and which `--cert` file formats are accepted. The decision is recorded as a
new ADR under `Documentation/Planning/Decisions/`.

## Context

The exit codes are the same in every curl build (35 `CURLE_SSL_CONNECT_ERROR`,
58 `CURLE_SSL_CERTPROBLEM`, 59 `CURLE_SSL_CIPHER`, 60 `CURLE_PEER_FAILED_VERIFICATION`,
77 `CURLE_SSL_CACERT_BADFILE`; <https://curl.se/libcurl/c/libcurl-errors.html>), and
BL-062 implements them without waiting for this decision. What differs by TLS build
is everything else. The reference binary in `Documentation/Product/Product-Overview.md`
is an Schannel build; curl on Linux and macOS is usually an OpenSSL (or other) build, and
.NET's `SslStream` sits on Schannel on Windows and on OpenSSL on Linux. Curl publishes
native binaries for Windows, Linux and macOS (BL-028).

Measured 2026-09-26 with the local curl 8.21.0 (x86_64-w64-mingw32, libcurl/8.21.0
Schannel, Release-Date 2026-06-24) against `openssl s_server` on loopback with a
self-signed `CN=localhost` certificate, `-sS` given:

| Command | Exit | stderr |
| --- | --- | --- |
| `curl https://localhost:18443/` | 60 | `curl: (60) schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.` then `More details here: https://curl.se/docs/sslcerts.html`, a blank line, and three lines beginning `curl failed to verify the legitimacy of the server` |
| `curl --cacert cert.pem https://127.0.0.1:18443/` (name mismatch) | 60 | `curl: (60) schannel: CertFindExtension() returned no extension.` then the same five lines |
| `curl --tlsv1.3 -k` against a TLS 1.2-only server | 35 | `curl: (35) schannel: next InitializeSecurityContext failed: SEC_E_UNSUPPORTED_FUNCTION (0x80090302) - The function requested is not supported` |
| `curl -k --cert cert.pem --key key.pem` (PEM) | 58 | `curl: (58) schannel: Failed to import cert file cert.pem, last error is 0x80092002` |
| `curl -k --cert nonexist.pem` | 58 | `curl: (58) schannel: Failed to get certificate location or file for nonexist.pem` |
| `curl --capath . -k https://localhost:18445/` | 0 | `Warning: ignoring setting the CA path for the proxy, not supported by libcurl ` and `Warning: with Schannel` (two lines), and the path is not used |

The manpage (<https://curl.se/docs/manpage.html>, as published for curl 8.23.0 on
2026-09-26) says `--cert` "must be PEM format" and `--capath` uses "the specified
certificate directory to verify the peer"; the Schannel build does neither. The BCL can
honour both: `X509Certificate2.CreateFromPemFile` loads PEM, PKCS#12 loads through
`X509CertificateLoader`, and every certificate in a directory can go into
`X509ChainPolicy.CustomTrustStore`. The BCL cannot produce Schannel's
`SEC_E_*` text on Linux, nor OpenSSL's verify strings on Windows, except by mapping.

Choices to decide, for each of messages, `--capath` and `--cert` formats:

1. Match the Schannel build on every platform.
2. Match the platform's usual curl: Schannel on Windows, an OpenSSL build on Linux and
   macOS (name which OpenSSL-build version is the reference).
3. Honour the manpage (PEM `--cert`, working `--capath`) everywhere and accept that the
   Windows output differs from the Schannel build: a deliberate divergence.

Also state whether the default trust store is the operating system's store (what
`SslStream` uses by default, and what the Schannel build uses) on every platform.

## Acceptance criteria

- [ ] A new ADR in `Documentation/Planning/Decisions/` (next free number), status
      Accepted, records the choice for TLS failure message text, for `--capath`, for the
      accepted `--cert`/`--key` formats, and for the default trust store, each per
      platform.
- [ ] The ADR states, for each of those four, the exact behaviour BL-064 and BL-065 must
      test: for messages, the text for exit 35, 58, 60 and 77 or the rule that produces
      it; for `--capath`, honour or warn-and-ignore with the warning text.
- [ ] Any divergence from the reference Schannel build is named as a divergence in the
      ADR's Consequences.
- [ ] `Documentation/Planning/Decisions/README.md` lists the new ADR.

## Notes

**Decision (Stewart, 2026-09-26):** Option 2 - match the platform's usual curl. Windows: the Schannel build of curl 8.21.0 as measured above (its failure message text; `--capath` ignored with its two warning lines; `--cert` as that build behaves, PEM refused with exit 58). Linux and macOS: the OpenSSL build of curl 8.21.0 is the reference (its message text, a working `--capath`, PEM `--cert`); measure it before pinning text. Default trust store: the one the reference build uses on each platform. Where a measurement shows the platform's usual curl differs from this, the ADR records it.

Blocks BL-064 (messages and `--capath`) and BL-065 (`--cert`/`--key`). BL-062 and BL-063
proceed without it.

## Log

- 2026-09-26: Created.
- 2026-09-26: Stewart decided: match the platform's usual curl. Reassigned to Claude to record the ADR.
- 2026-09-26: Backlog -> Doing.
