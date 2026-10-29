# ADR-0874: The Schannel build ignores `--curves` and `--sigalgs`

- **Status:** Accepted
- **Date:** 2026-10-23
- **Supersedes in part:** ADR-0151 (the Schannel build's `--curves` and `--sigalgs` row) and
  ADR-0284 (the Schannel build's refusals, empty-list failures and handshake-failure text)

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-3305
(found by BL-3282).

## Context

ADR-0151 and ADR-0284 honoured `--curves` and `--sigalgs` on every platform with the text of
the build that applies them, measured against an earlier Windows reference (curl.se's LibreSSL
build for a `--curves` handshake failure). The reference on Windows is now curl 8.22.0's
Schannel build, which has no curves or signature-algorithm list support. Measured on
2026-10-23 with `Record-CurlExchange.ps1 -Tls` (a TLS 1.2 server with an RSA certificate) and
`curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel`, `-sSk` and:

| Option | Exit | Output |
| --- | --- | --- |
| `--curves nope` | 0 | the body |
| `--curves X25519` | 0 | the body |
| `--curves P-256:nope` | 0 | the body |
| `--sigalgs nope` | 0 | the body |
| `--sigalgs ecdsa_secp256r1_sha256` | 0 | the body |

Curl printed `curl: (59) failed setting curves list: 'nope'` for the first.

## Decision

When `HandBuiltTlsProvider` matches the Schannel build, `CurvesAndSignatureAlgorithms.Apply`
ignores both options, whatever their value: the ClientHello is the Schannel profile's, nothing
is refused (no exit 59) and no list is left empty (no exit 35), and a failed handshake prints
the Schannel build's text (`TlsFailureMessages.LibreSslHandshakeFailureAlert` is gone). The
OpenSSL build keeps ADR-0284 unchanged.

The options still route the transfer to the hand-built client (ADR-0140's table is untouched):
with the lists ignored it offers the Schannel profile, which is what the platform's curl sends,
so moving the route is not needed for the measured output and stays out of this change.

## Consequences

- `curl --curves nope https://...` on Windows completes as curl 8.22.0 Schannel does.
- Rows of `HandBuiltTlsProviderTests.CurvesAndSignatureAlgorithms` that pinned the Schannel
  build applying the lists are removed; one test pins that the Schannel build's hello is its
  profile under each measured value.
