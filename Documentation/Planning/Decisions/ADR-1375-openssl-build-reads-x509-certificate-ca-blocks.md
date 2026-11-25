# ADR-1375: The OpenSSL build reads X509 CERTIFICATE blocks in --cacert and --capath

- Status: Accepted
- Date: 2026-11-24
- Decided by Claude under Stewart's delegation
- Task: BL-5927 (follows ADR-1351 and ADR-1363)

## Context

`OpenSslCaFile` read `CERTIFICATE` and `TRUSTED CERTIFICATE` blocks and skipped OpenSSL's
older `X509 CERTIFICATE` label.

Measured with curl 8.18.0 (OpenSSL 3.5.5) on Ubuntu:

- A root held as an `X509 CERTIFICATE` block verifies from `--cacert` and from `--capath`
  (exit 0).
- An `X509 CERTIFICATE` block that cannot be decoded makes the whole `--cacert` file exit 77,
  wherever it stands. In a `--capath` file the blocks before it still count.
- Reject or trust settings following the certificate in a `CERTIFICATE` or `X509 CERTIFICATE`
  block are ignored from `--cacert` (exit 0) and honoured from `--capath` (exit 60,
  `certificate rejected (28)`).

## Decision

`OpenSslCaFile` reads all three labels in both places and counts all three toward the exit 77
check. It reads trust settings from `TRUSTED CERTIFICATE` blocks only in `--cacert`, and from
any of the three in `--capath`. The Schannel build is unchanged.

## Why

It is what the platform's curl does: OpenSSL reads `--cacert` with
`PEM_X509_INFO_read_bio` and `--capath` with `PEM_read_bio_X509_AUX`, which differ in
exactly this way, and one flag in the existing reader matches it.
