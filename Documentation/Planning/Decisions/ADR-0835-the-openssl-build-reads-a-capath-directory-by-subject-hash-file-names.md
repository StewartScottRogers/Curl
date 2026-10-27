# ADR-0835: The OpenSSL build reads a --capath directory by subject hash file names

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3640)
- Relates to: [ADR-0009](ADR-0009-tls-behaviour-matches-the-platforms-usual-curl-build.md)

## Context

CGR-111: curl 8.18.0's OpenSSL build on Linux exits 60 with `SSL certificate OpenSSL verify
result: self-signed certificate (18)` for `--capath plain`, `SSL_CERT_DIR=plain` and
`SSL_CERT_DIR=/nope:plain`, where `plain/` holds the server's self-signed certificate as
`server-ca.crt`; with `--capath hashdir`, the same certificate after `openssl rehash`, it exits 0.
OpenSSL's hashed directory lookup (`crypto/x509/by_dir.c`) splits the value at the platform's list
separator (`:`, `;` on Windows), and for the issuer it needs opens `<hash>.0`, `<hash>.1` and on
until a number is missing, `<hash>` being the issuer name's `X509_NAME_hash` in eight lowercase hex
digits, and takes from those files only certificates whose subject is that name. curl's `--capath`
documentation requires the directory to be prepared with `c_rehash`. Curl read every file in the
one directory named and trusted every certificate in them, so all three repros exited 0.

## Decision

1. `OpenSslSubjectNameHash.Of` computes `X509_NAME_hash` as OpenSSL does: each relative
   distinguished name's DER `SET` with its UTF8String, PrintableString, T61String, IA5String,
   VisibleString, UniversalString and BMPString values made canonical (UTF-8, outer whitespace
   dropped, inner runs made one space, ASCII lowercased, written as UTF8String), concatenated
   without the outer `SEQUENCE`; the first four bytes of its SHA-1 read little-endian. Its tests
   pin values measured with `openssl x509 -noout -subject_hash` (ISRG Root X1 `4042bcee`,
   `/CN=localhost` `ce275665`).
2. `ServerCertificateVerification` (OpenSSL build) splits `--capath` at `Path.PathSeparator`,
   skipping empty entries; in each directory it takes every file name of eight lowercase hex digits,
   a dot and decimal digits, and for each such hash reads `<hash>.0` upward until a number is
   missing, trusting only the certificates in them whose subject hashes to that hash. Any other
   file adds nothing.
3. The lookup trusts every correctly hashed certificate up front rather than looking up each issuer
   as the chain is built; the trust is the same, since a certificate is reached only through its own
   subject's hash. CRL files (`<hash>.r<n>`) in the directory are not read, as before.

## Consequences

A certificate copied into a `--capath` or `SSL_CERT_DIR` directory without `openssl rehash` is no
longer trusted, so such a transfer ends in exit 60 as with curl's OpenSSL build. The Schannel build
still ignores `--capath`.
