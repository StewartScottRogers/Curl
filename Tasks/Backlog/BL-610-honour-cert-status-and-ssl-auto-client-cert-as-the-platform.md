---
id: BL-610
title: Honour --cert-status and --ssl-auto-client-cert as the platform's curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-607]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-610 — Honour --cert-status and --ssl-auto-client-cert as the platform's curl does

## Goal

`--cert-status` (OCSP stapling check, exit 91 `CURLE_SSL_INVALIDCERTSTATUS`) and `--ssl-auto-client-cert` (Schannel's automatic client certificate) behave as each platform's curl 8.21.0 build does: honoured where the build honours them and the BCL can, refused with the build's exit code and message where it refuses them; and whether any Curl option can produce exit 83 (`CURLE_SSL_ISSUER_ERROR`) is measured and recorded.

## Context

- Conformance audit 2026-09-28, row 17 (Major; exits 83 and 91 never produced). Option: BL-607.
- Schannel builds usually refuse `--cert-status`; OpenSSL builds check the stapled OCSP response. The BCL exposes no stapled response on `SslStream`, so off Windows record what is possible and, if not, what Curl prints instead, in an ADR marked "Decided by Claude under Stewart's delegation".
- `--ssl-auto-client-cert` maps to letting SChannel pick a certificate from the user store; `SslClientAuthenticationOptions.LocalCertificateSelectionCallback` with the store is the BCL route on Windows.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Tls -k`: `--cert-status` and `--ssl-auto-client-cert` on Windows and on Linux or macOS; stderr and exit code copied into Notes, plus a note on exit 83 (the CLI has no issuer-certificate option in 8.21.0 if that is what the manual shows).
- [ ] Tests pin each platform's measured behaviour under `OSCondition`.
- [ ] Any decision the BCL forces is recorded in an ADR as above, indexed in `Documentation/Planning/Decisions/README.md`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
