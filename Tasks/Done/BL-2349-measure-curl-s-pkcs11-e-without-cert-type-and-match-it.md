---
id: BL-2349
title: Measure curl's pkcs11: -E without --cert-type and match it
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-2324]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2349 — Measure curl's pkcs11: -E without --cert-type and match it

## Goal

Measure real curl (Schannel and OpenSSL builds) with `-E pkcs11:...` and `--key pkcs11:...` and no `--cert-type`/`--key-type` (curl's tool sets `ENG` then), and make `EasyOptionsFromCommandLine.TlsOf` and `ClientCertificateLoader` match it. ADR-0530 dropped the `ENG` guess so BL-2324 changed no behaviour.

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] `EasyOptionsFromCommandLineTests.Tls` pins `ENG` for a `pkcs11:` `--cert`, `--key`, `--proxy-cert` and `--proxy-key` without a type, a given type kept, and no type for a file.
- [x] `SslStreamTlsProviderTests.ClientCertificateTypes` pins the OpenSSL build's exit 58 `Failed to initialize provider: ...could not load the shared library` for a `pkcs11:` certificate or key of type `ENG` or `PROV`, and the Schannel build's missing-file exit 58 for a `pkcs11:` certificate of type `ENG`.
- [x] ADR-0537 records the measurements and the decision.
- [x] No option added or changed, so `--ai-help` is unchanged.

## Notes

- Measured 2026-10-15 against a loopback TLS server (`Record-CurlExchange.ps1 -Tls`; `openssl s_server` in WSL): curl 8.21.0 Schannel, curl 8.18.0 LibreSSL 4.2.1 (curl.se Windows), curl 8.18.0 OpenSSL 3.5.5 (Ubuntu, WSL). All set `ENG` (`--libcurl`). Schannel: `schannel: Failed to get certificate location or file for <value>`. OpenSSL 3.5.5: `Failed to initialize provider: error:12800067:DSO support routines::could not load the shared library` (exit 58), for the cert and for a `pkcs11:` key. LibreSSL: `SSL_FILETYPE_ENGINE not supported for certificate`, not matched (ADR-0537: the OpenSSL-API loader keeps the OpenSSL 3 texts).
- Curl's built `curl.exe` now prints the Schannel line byte for byte for `-E pkcs11:token=a;object=b` and `--cert nofile.pem --key pkcs11:object=k`.
- Measure-CodeQuality: Curl.Console and Curl.Networking.UnitLibrary 100% line and branch, worst CRAP 10.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. pkcs11: -E/--key without a type is ENG as curl's tool sets it; each build's failure matched (ADR-0537)
