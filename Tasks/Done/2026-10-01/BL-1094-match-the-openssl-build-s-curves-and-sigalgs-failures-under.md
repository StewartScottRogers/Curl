---
id: BL-1094
title: Match the OpenSSL build's --curves and --sigalgs failures under a TLS 1.2 ceiling
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1094 — Match the OpenSSL build's --curves and --sigalgs failures under a TLS 1.2 ceiling

## Goal

Under `--tls-max 1.2`, `--curves '?bogus'` sends a ClientHello as the OpenSSL build does, and `--sigalgs RSA+SHA1` fails with OpenSSL's `no ciphers available` line, instead of Curl's current `no suitable groups` and `no suitable signature algorithm` failures.

## Context

- Measured 2026-10-01 (BL-1087) with `Record-CurlExchange.ps1 -Curl wsl.exe -ListenAddress 172.26.96.1 -Port <p>`, Ubuntu's curl 8.18.0 with OpenSSL 3.5.5:
  - `--tls-max 1.2 --curves '?bogus'`: a ClientHello is sent (`16 03 01 00 b0 01 ...`); against the recorder it ends exit 35 `wrong version number`. Capture the ClientHello's `supported_groups` (probably absent) against a TLS server before pinning.
  - `--tls-max 1.2 --sigalgs RSA+SHA1`: exit 35 `curl: (35) TLS connect error: error:0A0000B5:SSL routines::no ciphers available`, writing `15 03 01 00 02 02 50` first (ADR-0303).
- `CurvesAndSignatureAlgorithms.Offer` (Curl.Networking.UnitLibrary) fails both today; ADR-0284 and ADR-0303 describe the failures.

## Acceptance criteria

- [x] `HandBuiltTlsProviderTests` pin both measured cases under a TLS 1.2 ceiling: the ClientHello sent for `?bogus`, and exit 35 with the `no ciphers available` line and the alert bytes for `RSA+SHA1`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports no failing member.

## Notes

- Measured 2026-10-01 against Ubuntu's curl 8.18.0 / OpenSSL 3.5.5 (`Record-CurlExchange.ps1 -Curl wsl.exe`): under `--tls-max 1.2`, `--curves '?bogus'` and `--curves X25519MLKEM768` send the ordinary TLS 1.2 ClientHello minus `ec_point_formats` and `supported_groups` (suites unchanged); `--sigalgs RSA+SHA1` and `mldsa65` (also with `?bogus`) fail exit 35 `no ciphers available` after `15 03 01 00 02 02 50`; `ed25519`/`ed448` send a hello. ADR-0310 records the rule.
- Added `Curl.Tls.UnitLibrary` and `Curl.Tls.UnitTests` to `touches`: `Tls12ClientHelloBuilder` always wrote both group extensions. No task in Doing on `origin/work/dark-factory` named them (BL-1036 touches Ssh only; BL-976 File only).
- `CurvesAndSignatureAlgorithms.Offer` now splits on the range: TLS 1.3 keeps ADR-0284's three checks; below it only "no scheme `IsTls12Scheme` accepts" fails, with `TlsFailureMessages.OpenSslNoCiphersAvailable`.
- Not covered: with `--sigalgs ed25519` alone real curl also drops RSA-authenticated suites (shorter hello); Curl keeps the profile's suites.
- Gates: build `-warnaserror` clean, fast tests green, `Measure-CodeQuality.ps1` 0 failing members for Curl.Networking.UnitLibrary and Curl.Tls.UnitLibrary.
## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Under --tls-max 1.2, --curves leaving no group sends the ClientHello without group extensions and --sigalgs leaving no TLS 1.2 scheme fails with OpenSSL's no ciphers available
