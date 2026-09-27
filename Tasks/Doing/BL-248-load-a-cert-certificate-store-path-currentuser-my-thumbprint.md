---
id: BL-248
title: Load a --cert certificate-store path (CurrentUser\MY\<thumbprint>) in the Schannel build
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-065]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed:
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

- [ ] A named test asserts the in-memory server receives a certificate found through the
      store seam for `CurrentUser\MY\<thumbprint>` in the Schannel build.
- [ ] A named test pins the measured exit code and message for a store path whose
      thumbprint is not found.
- [ ] The OpenSSL build still reads the same value as a file path (named test).
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

Filed as a follow-up by BL-065.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
