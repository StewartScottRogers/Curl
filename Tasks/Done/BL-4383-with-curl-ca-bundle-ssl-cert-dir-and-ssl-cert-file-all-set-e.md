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
completed: 2026-10-30
---
# BL-4383 — With CURL_CA_BUNDLE, SSL_CERT_DIR and SSL_CERT_FILE all set empty, Curl's OpenSSL build trusts nothing (exit 60) where curl uses its built-in CA bundle (exit 0)

## Goal

On linux and macOS, with `CURL_CA_BUNDLE`, `SSL_CERT_DIR` and `SSL_CERT_FILE` all set to the empty string, an https transfer Curl makes with no `--cacert` or `--capath` trusts the CA bundle curl would trust and answers as curl does (exit 0 against a publicly trusted server).

## Context

- Measured by BL-4380 in WSL Ubuntu: `env CURL_CA_BUNDLE= SSL_CERT_DIR= SSL_CERT_FILE= curl -sS -o /dev/null https://example.com/` gives exit 0 with Debian's curl 8.18.0 (OpenSSL 3.5.5), whose built-in CA bundle (`--with-ca-bundle`) does not depend on the environment. A self-contained linux-x64 build of Curl gives exit 60 (`unable to get local issuer certificate (20)`).
- Cause: Curl treats the empty variables as unset (ADR-0707), so it falls back to .NET's system store. On Linux .NET's OpenSSL store reads `SSL_CERT_FILE` and `SSL_CERT_DIR` from the process environment itself, and an empty value leaves the store with nothing in it.
- Check first what curl 8.22.0 does when built from the release tarball (configure finds `/etc/ssl/certs/ca-certificates.crt` itself), then decide how the OpenSSL build finds its default bundle and record that in an ADR. Start at `ServerCertificateVerification.ReadTrustAnchors`.

## Acceptance criteria

- [x] With all three variables empty, Curl's OpenSSL build off Windows trusts the same default CA bundle curl does, pinned by a unit test that runs on every platform.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Decision (ADR-1168): in the OpenSSL build, without `--cacert`, when `SSL_CERT_FILE` or `SSL_CERT_DIR` is set empty, `ServerCertificateVerification.DefaultCaBundleFile` is the first file of curl's `configure` search list that exists (`/etc/ssl/certs/ca-certificates.crt`, `/etc/pki/tls/certs/ca-bundle.crt`, `/usr/share/ssl/certs/ca-bundle.crt`, `/usr/local/share/certs/ca-root-nss.crt`, `/etc/ssl/cert.pem`), read exactly as a `--cacert` file (custom root store, partial-chain anchoring, exit 77 naming it). Only that case changes: unset variables leave .NET reading OpenSSL's default paths (the same bundle), non-empty ones are already `--cacert`/`--capath` (ADR-0707). Narrower than "always use the bundle" so the existing system-store tests (e.g. `ServerCertificateRevocationTests.ReadTrustAnchors_SystemStoreInTheOpenSslBuild_ChecksNoRevocation`) keep holding on Linux CI.
- curl 8.22.0 could not be built from the tarball in a lane; its `configure` (`CURL_CHECK_CA_BUNDLE`) searches the list above, so it trusts the same file Debian's build does. Recorded in the ADR.
- The environment and the candidate list are internal seams (`ReadEnvironmentVariable`, `DefaultCaBundleCandidates`), so the tests in `SslStreamTlsProviderTests` pin it on every platform with a temporary bundle; platform-neutral, so CI on Linux and macOS is covered by the green fast run here.
- Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary: 0 failing members. `dotnet build -warnaserror` clean; fast tests green (114 test assemblies passed).



## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 79bbee99 7ac22caa.
- 2026-10-30: Doing -> Done. With SSL_CERT_FILE or SSL_CERT_DIR set empty, the OpenSSL build trusts curl's built-in CA bundle (first of configure's list) instead of an emptied .NET store
