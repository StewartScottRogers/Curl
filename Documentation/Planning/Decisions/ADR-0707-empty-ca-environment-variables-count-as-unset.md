# ADR-0707: An empty CURL_CA_BUNDLE, SSL_CERT_DIR or SSL_CERT_FILE counts as unset

- Status: Accepted
- Date: 2026-10-19
- Decided by Claude under Stewart's delegation
- Task: BL-2822 (gap finding GF-0097)

## Context

The gap analysis office measured curl 8.22.0's OpenSSL build on Linux with one of `SSL_CERT_DIR` and
`SSL_CERT_FILE` naming the test CA and the other set to the empty string: curl completed the transfer
(exit 0, no stderr), and through an HTTPS proxy to `gap.invalid` it failed the handshake with exit 35.
Curl copied the empty value verbatim into `--capath` or `--cacert` (ADR-0682), and the empty path
reached `Directory.GetFiles("")` or `File.ReadAllText("")`, whose `ArgumentException` escaped every
catch: an unhandled exception, exit 134, before or right after the CONNECT.

A lane cannot run Linux curl, so the measurement in the finding is the evidence.

## Decision

- `CaCertificateEnvironment.Apply` treats a variable set to the empty string as unset, for all three
  variables, so an empty `SSL_CERT_FILE` beside a usable `SSL_CERT_DIR` (or the reverse) leaves only the
  usable one, which gives the measured exit 0.
- `ServerCertificateVerification` never lets an empty or malformed path escape: a `--capath` that
  `Directory.GetFiles` refuses with `ArgumentException` or `NotSupportedException` adds nothing, as a
  missing one already did; an empty `--cacert` names no file and is exit 77, as any unreadable one is.

## Consequences

- No CA path from the environment can crash a transfer; the command-line forms are caught too.
- If a later measurement shows curl treats an empty `CURL_CA_BUNDLE` as set (skipping the other two),
  only `CaCertificateEnvironment` changes.
