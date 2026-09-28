---
id: BL-606
title: Present a client certificate and apply cipher lists on the HTTPS proxy's TLS handshake
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-605]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-606 — Present a client certificate and apply cipher lists on the HTTPS proxy's TLS handshake

## Goal

The TLS handshake with an `https://` proxy presents `--proxy-cert`/`--proxy-key` (with `--proxy-cert-type`, `--proxy-key-type`, `--proxy-pass`) and applies `--proxy-ciphers` and `--proxy-tls13-ciphers`, exactly as the origin handshake applies their counterparts, with the same failures and exit codes.

## Context

- Conformance audit 2026-09-28, row 15 (Major). Options: BL-605.
- The proxy handshake has its own TLS options (ADR-0095, ADR-0061); the origin versions are `ClientCertificateLoader.cs`, `CipherSuitesPolicyFactory.cs`, `TlsClientOptions.cs` in `Curl.Networking.UnitLibrary`, mapped by `Curl.Console/TlsClientOptionsMapping.cs`. Cipher lists follow ADR-0011 (Schannel ignores or refuses as measured; honoured elsewhere).

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Tls` as the proxy: `-x https://127.0.0.1:<P> --proxy-insecure --proxy-cert missing.pem`, a wrong `--proxy-pass`, and `--proxy-ciphers BOGUS`; stderr and exit code copied into Notes, per platform where they differ.
- [ ] Tests on the options mapping show each proxy option reaching the proxy's TLS options and not the origin's, and the measured failures, pinned per platform with `OSCondition` where they differ.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
