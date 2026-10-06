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
completed: 2026-09-30
---
# BL-606 — Present a client certificate and apply cipher lists on the HTTPS proxy's TLS handshake

## Goal

The TLS handshake with an `https://` proxy presents `--proxy-cert`/`--proxy-key` (with `--proxy-cert-type`, `--proxy-key-type`, `--proxy-pass`) and applies `--proxy-ciphers` and `--proxy-tls13-ciphers`, exactly as the origin handshake applies their counterparts, with the same failures and exit codes.

## Context

- Conformance audit 2026-09-28, row 15 (Major). Options: BL-605.
- The proxy handshake has its own TLS options (ADR-0095, ADR-0061); the origin versions are `ClientCertificateLoader.cs`, `CipherSuitesPolicyFactory.cs`, `TlsClientOptions.cs` in `Curl.Networking.UnitLibrary`, mapped by `Curl.Console/TlsClientOptionsMapping.cs`. Cipher lists follow ADR-0011 (Schannel ignores or refuses as measured; honoured elsewhere).

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Tls` as the proxy: `-x https://127.0.0.1:<P> --proxy-insecure --proxy-cert missing.pem`, a wrong `--proxy-pass`, and `--proxy-ciphers BOGUS`; stderr and exit code copied into Notes, per platform where they differ.
- [x] Tests on the options mapping show each proxy option reaching the proxy's TLS options and not the origin's, and the measured failures, pinned per platform with `OSCondition` where they differ.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-09-30, curl 8.21.0 Schannel (Windows), `Record-CurlExchange.ps1 -Tls -Port 47606`, each with
  `-sS -x https://127.0.0.1:47606 --proxy-insecure ... http://example.com/`:
  - `--proxy-cert missing.pem`: `curl: (58) schannel: Failed to get certificate location or file for missing.pem`, exit 58.
  - `--proxy-cert client.p12 --proxy-cert-type P12 --proxy-pass wrong` (a real PKCS#12 with another password):
    `curl: (58) schannel: Failed to import cert file <path>, password is bad`, exit 58.
  - `--proxy-ciphers BOGUS`: `curl: (59) schannel: Failed setting algorithm cipher list`, exit 59.
  - `--proxy-tls13-ciphers BOGUS`: exit 0; without `-s`, `Warning: ignoring --proxy-tls13-ciphers, not supported by libcurl with Schannel`.
  - Each is byte for byte what the origin's counterpart (`--cert`, `--pass`, `--ciphers`, `--tls13-ciphers`) prints, measured the same day.
  No OpenSSL build was at hand; since curl's proxy handshake reuses the origin's code with the proxy's config, the
  OpenSSL build is pinned by requiring the proxy's failure to equal the origin's, whose OpenSSL messages the
  Networking tests already pin.
- Design: the proxy's TLS provider was already the origin's provider class built from its own `TlsClientOptions`
  (`CurlComposition.CreateTlsProvider`), so the whole change is `TlsClientOptionsMapping.ProxyFromCommandLine`
  copying the seven options verbatim, as `FromCommandLine` copies the counterparts. `--proxy-cert`'s
  `cert[:password]` split then happens where the origin's does, in `ClientCertificateLoader`. No new decision, so no ADR.
  `Curl.Networking.UnitLibrary` needed no change.
- Tests: `TlsClientOptionsMappingTests` (each proxy option reaches the proxy's options and not the origin's, and the
  reverse); `CurlCompositionTests.ProxyTlsHandshake.cs` (the three measured failures pinned on Windows, and on every
  platform the proxy's failure equals the origin's; the proxy options leave the origin's handshake alone).
  `CurlCompositionTests` became partial for it.
- Follow-up: curl prints neither `--tls13-ciphers` nor `--proxy-tls13-ciphers`'s Schannel "ignoring" warning; filed as BL-1034.

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. --proxy-cert/--proxy-key/--proxy-cert-type/--proxy-key-type/--proxy-pass and --proxy-ciphers/--proxy-tls13-ciphers now reach the HTTPS proxy's TLS handshake and fail exactly as their origin counterparts do
