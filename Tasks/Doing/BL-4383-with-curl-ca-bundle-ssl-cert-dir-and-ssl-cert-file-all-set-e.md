---
id: BL-4383
title: With CURL_CA_BUNDLE, SSL_CERT_DIR and SSL_CERT_FILE all set empty, Curl's OpenSSL build trusts nothing (exit 60) where curl uses its built-in CA bundle (exit 0)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitTests/SslStreamTlsProviderTests.cs]
requirement: none
created: 2026-10-30
completed:
---
# BL-4383 — With CURL_CA_BUNDLE, SSL_CERT_DIR and SSL_CERT_FILE all set empty, Curl's OpenSSL build trusts nothing (exit 60) where curl uses its built-in CA bundle (exit 0)

## Goal

On linux and macOS, with `CURL_CA_BUNDLE`, `SSL_CERT_DIR` and `SSL_CERT_FILE` all set to the empty string, an https transfer Curl makes with no `--cacert` or `--capath` trusts the CA bundle curl would trust and answers as curl does (exit 0 against a publicly trusted server).

## Context

- Measured by BL-4380 in WSL Ubuntu: `env CURL_CA_BUNDLE= SSL_CERT_DIR= SSL_CERT_FILE= curl -sS -o /dev/null https://example.com/` gives exit 0 with Debian's curl 8.18.0 (OpenSSL 3.5.5), whose built-in CA bundle (`--with-ca-bundle`) does not depend on the environment. A self-contained linux-x64 build of Curl gives exit 60 (`unable to get local issuer certificate (20)`).
- Cause: Curl treats the empty variables as unset (ADR-0707), so it falls back to .NET's system store. On Linux .NET's OpenSSL store reads `SSL_CERT_FILE` and `SSL_CERT_DIR` from the process environment itself, and an empty value leaves the store with nothing in it.
- Check first what curl 8.22.0 does when built from the release tarball (configure finds `/etc/ssl/certs/ca-certificates.crt` itself), then decide how the OpenSSL build finds its default bundle and record that in an ADR. Start at `ServerCertificateVerification.ReadTrustAnchors`.

## Acceptance criteria

- [ ] With all three variables empty, Curl's OpenSSL build off Windows trusts the same default CA bundle curl does, pinned by a unit test that runs on every platform.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes



## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 79bbee99 7ac22caa.
