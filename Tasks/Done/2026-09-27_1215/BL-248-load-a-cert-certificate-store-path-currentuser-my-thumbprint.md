---
id: BL-248
title: Load a --cert certificate-store path (CurrentUser\MY\<thumbprint>) in the Schannel build
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-065]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-248 — Load a --cert certificate-store path (CurrentUser\MY\<thumbprint>) in the Schannel build

## Goal

In the Schannel build, a `--cert` value naming a Windows certificate store location
(`<store location>\<store name>\<thumbprint>`, for example `CurrentUser\MY\<40 hex digits>`)
presents that certificate from the store, as curl's Schannel build does, instead of being
read as a file path.

## Context

- BL-065 made `SslStreamTlsProvider` present a `--cert` client certificate; its Schannel
  build loads files only (`ClientCertificateLoader.LoadAsSchannelBuild`) and reports a path it
  cannot open as `schannel: Failed to get certificate location or file for <path>`.
- curl's Schannel build tries the store form first (`get_cert_location` in
  `lib/vtls/schannel.c`) and falls back to a file only when the value is not a store
  location. The manpage (<https://curl.se/docs/manpage.html>, curl 8.23.0) describes
  `--cert` "CurrentUser\MY\934a7ac6f8a5d579285a74fa61e19f23ddfe8d7a" for Schannel.
- Measure curl 8.21.0 (Schannel, Windows) with a certificate in `CurrentUser\MY` before
  pinning: the accepted location names, whether the thumbprint is case-insensitive, and
  the exit code and message for a store path whose thumbprint is not in the store.
- BCL: `X509Store(StoreName, StoreLocation)` and `Certificates.Find(FindByThumbprint, ...)`.
  Tests must not depend on the machine's store contents; put a seam in front of the store.

## Acceptance criteria

- [x] A named test asserts the in-memory server receives a certificate found through the
      store seam for `CurrentUser\MY\<thumbprint>` in the Schannel build.
- [x] A named test pins the measured exit code and message for a store path whose
      thumbprint is not found.
- [x] The OpenSSL build still reads the same value as a file path (named test).
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

Filed as a follow-up by BL-065.

- Measured curl 8.21.0 (Schannel, Windows) on 2026-09-27 with a self-signed certificate put
  in `CurrentUser\MY` for the run and removed after; the table is in ADR-0066. Thumbprint
  and store name match ignoring case, the location name is a case-sensitive prefix match
  (`Current\MY\...` works, `currentuser\MY\...` is read as a file), `--cert-type` and the
  passphrase are ignored. Not in store: exit 58 `schannel: client cert not found in cert
  store`. No such store: exit 58 `schannel: Failed to open cert store 10000 <name>, last
  error is 0x00000002`. Non-hex thumbprint: exit 58 with curl's own text.
- Design: `ClientCertificateStorePath` parses as `get_cert_location` does;
  `IClientCertificateStore` is the seam (fake in tests, `SystemClientCertificateStore` over
  `X509Store` in production), passed through a new internal `SslStreamTlsProvider`
  constructor. Decision recorded in ADR-0066 (decided by Claude under Stewart's delegation):
  the six locations `X509Store` cannot reach report the open failure rather than
  P/Invoke `CertOpenStore`.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0066 and its README row;
  no task in Doing touches it.
- Tests: `AuthenticateAsClientAsync_WithAStorePathInTheSchannelBuild_PresentsTheCertificateFromTheStore`,
  `AuthenticateAsClientAsync_WithAStorePathWhoseThumbprintIsNotInTheStoreInTheSchannelBuild_FailsWithSslCertProblem`,
  `AuthenticateAsClientAsync_WithAStorePathInTheOpenSslBuild_ReadsItAsAFile`, plus
  `ClientCertificateStorePathTests` and `SystemClientCertificateStoreTests` (36 new, all green).

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. The Schannel build presents a --cert CurrentUser\MY\<thumbprint> certificate from the Windows store, with curl 8.21.0's exit 58 messages
