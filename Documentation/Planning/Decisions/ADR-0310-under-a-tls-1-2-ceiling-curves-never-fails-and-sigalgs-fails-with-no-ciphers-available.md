# ADR-0310 — Under a TLS 1.2 ceiling `--curves` never fails and `--sigalgs` fails with `no ciphers available`

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1094.

## Context

ADR-0284 fails a `--curves` list leaving no group with `no suitable groups` and a `--sigalgs`
list leaving no checkable scheme with `no suitable signature algorithm`, whatever the version
range. Ubuntu's curl 8.18.0 with OpenSSL 3.5.5 does not under `--tls-max 1.2`, measured
2026-10-01 with `Record-CurlExchange.ps1 -Curl wsl.exe -ListenAddress 172.26.96.1`:

- `--curves '?bogus'` and `--curves X25519MLKEM768` (a TLS 1.3-only group) send a ClientHello
  that is the ordinary TLS 1.2 one without `ec_point_formats` and `supported_groups`; the cipher
  suites, ECDHE ones included, are unchanged.
- `--sigalgs RSA+SHA1` and `--sigalgs mldsa65` (with or without `--curves '?bogus'`) fail with
  exit 35 `TLS connect error: error:0A0000B5:SSL routines::no ciphers available`, after the
  internal_error alert `15 03 01 00 02 02 50` (ADR-0303).
- `--sigalgs ed25519` and `ed448` send a ClientHello.

## Decision

When the range does not reach TLS 1.3, `CurvesAndSignatureAlgorithms` fails only when the list
leaves no scheme TLS 1.2 can check (`TlsSignatureScheme.IsTls12Scheme`), with OpenSSL's
`no ciphers available` line and ADR-0303's alert. A list leaving no group is no failure, and
`Tls12ClientHelloBuilder` leaves out both group extensions when it has no group. Both builds do
this, as ADR-0284 has both apply the options through the hand-built client.

## Consequences

- The TLS 1.2 failures and ClientHello match the OpenSSL build for the measured cases.
- With `--sigalgs ed25519` alone real curl also drops the RSA-authenticated suites (its
  ClientHello is shorter); Curl keeps the profile's suites. Not covered here.

## Alternatives considered

- **Keep ADR-0284's failures under every range.** Lost: measured otherwise.
