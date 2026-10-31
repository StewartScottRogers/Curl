# ADR-1168: The OpenSSL build trusts its built-in CA bundle when the environment empties the system store

- Status: Accepted
- Date: 2026-10-30
- Decided by Claude under Stewart's delegation
- Task: BL-4383 (measured by BL-4380)

## Context

BL-4380 measured in WSL Ubuntu: `env CURL_CA_BUNDLE= SSL_CERT_DIR= SSL_CERT_FILE= curl -sS -o /dev/null
https://example.com/` is exit 0 with Debian's curl 8.18.0 (OpenSSL 3.5.5). Its CA bundle is built in
(`--with-ca-bundle`); curl's `configure` finds the same file itself when built from the release tarball,
taking the first of `/etc/ssl/certs/ca-certificates.crt`, `/etc/pki/tls/certs/ca-bundle.crt`,
`/usr/share/ssl/certs/ca-bundle.crt`, `/usr/local/share/certs/ca-root-nss.crt` and `/etc/ssl/cert.pem`
that exists. curl reads that bundle as its CA file and does not ask OpenSSL's default paths, so the
environment does not change what it trusts. A lane cannot build curl 8.22.0 on Linux; its `configure`
search list is the one above, so the tarball build trusts the same file.

Curl treats the empty variables as unset (ADR-0707) and, without `--cacert`, falls back to .NET's
system store. On Linux .NET's OpenSSL store reads `SSL_CERT_FILE` and `SSL_CERT_DIR` from the process
environment itself, and an empty value leaves it with nothing: exit 60, `unable to get local issuer
certificate (20)`.

## Decision

- In the OpenSSL build, without `--cacert` (file or blob), when `SSL_CERT_FILE` or `SSL_CERT_DIR` is
  set to the empty string, `ServerCertificateVerification` trusts the first of `configure`'s candidates
  that exists (`DefaultCaBundleFile`), read exactly as a `--cacert` file is: a custom root store with
  any `--capath` roots beside it, the partial-chain anchoring, and exit 77 naming the bundle when it
  cannot be read, as curl names its CA file.
- Otherwise the system store stays: with the variables unset .NET's store reads OpenSSL's default
  paths, which hold the same distribution bundle; set non-empty, ADR-0707's `CaCertificateEnvironment`
  has already made them `--cacert` and `--capath`. Changing only the case that diverges keeps every
  other path as it is.
- The Schannel build is unchanged: curl's Schannel build uses the Windows store.
- The candidates and the environment are read through internal seams, so the unit tests pin the
  behaviour on every platform.

## Consequences

- An https transfer with the three variables empty trusts what curl trusts on Linux and macOS.
- With no candidate present (a system without a CA bundle) the system store is still used, and the
  transfer fails with exit 60 as before.
