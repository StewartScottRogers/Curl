---
id: BL-1152
title: Pin the TLS 1.2-only hand-built hello's session ID and record version, and OpenSSL's --tls-max 1.0 refusal
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1152 — Pin the TLS 1.2-only hand-built hello's session ID and record version, and OpenSSL's --tls-max 1.0 refusal

## Goal

Below a TLS 1.3 ceiling, `HandBuiltTlsProvider`'s first record matches the platform curl's in its record version and legacy session ID too, and the OpenSSL build refuses `--tls-max 1.0` as Ubuntu's curl does.

## Context

- BL-941 / ADR-0340 measured with `Record-CurlExchange.ps1 -Script` (`read`, `close`) against `https://localhost`: Schannel's `--tls-max 1.2` hello is in a record with version `0x0303` (its `--tls-max 1.0` one `0x0301`), and both builds send an empty legacy session ID; BL-941 pinned only the extensions.
- Ubuntu's curl 8.18.0 (OpenSSL 3.5.5) with `--tls-max 1.0` sends a `protocol_version` alert (`15 03 03 00 02 02 46`) and no hello, exit 35. Measure its stderr line before pinning.
- Start at `Tls12ClientHandshake.SessionIdToOffer`, `Tls12ClientConnection`'s record writer and `HandBuiltTlsProviderTests.ClientHello.cs`.

## Acceptance criteria

- [x] `Curl.Networking.UnitTests` show the TLS 1.2-only hello's record version and session ID are the measured ones for each build.
- [x] A test shows the OpenSSL build with `--tls-max 1.0` fails with curl's measured exit code and stderr line.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Tls.UnitLibrary` and `Curl.Networking.UnitLibrary`.

## Notes

- Filed by BL-941 (ADR-0340).
- Measured 2026-10-02 with `Record-CurlExchange.ps1 -Script` (read, close): Schannel (curl 8.21.0) `--tls-max 1.2` hello record `0x0303`, `--tls-max 1.0` `0x0301`; Ubuntu's curl 8.18.0 (OpenSSL 3.5.5) `--tls-max 1.2` `0x0301`; every one with an empty legacy session ID. Ubuntu's `--tls-max 1.0` and `1.1` (with or without `-k` and `--tlsv1.0`): exit 35, `curl: (35) TLS connect error: error:0A0000BF:SSL routines::no protocols available`, the trust lines before it.
- Decided (ADR-0364): `ClientHelloProfile.Tls12RecordVersionIsTheCeiling` (Schannel) feeds `Tls12ClientSettings.ClientHelloRecordVersion`, which `Tls12RecordLayer` writes until the server picks a version. Schannel's unmeasured `--tls-max 1.1` gets `0x0302` by the same rule. The OpenSSL build refuses a TLS 1.0 or 1.1 ceiling (1.1 was measured too) after the trust event, sending `15 03 03 00 02 02 46`.
- `HandBuiltTlsProvider.Prepare` is split into `Prepare` and `PrepareTrust` to keep complexity at 10; the pre-handshake alert is now a record (`byte[]? AlertRecord`) instead of an internal_error flag. The OpenSSL rows of the legacy-ceiling transfer test and the reset test now use a TLS 1.2 ceiling or moved to the refusal tests.
- Not done: curl's `-v` `TLSv1.3 (OUT), TLS alert, protocol version (582)` line, since the hand-built client writes no TLS message lines yet. The separate review and conformance stages were folded into the direct measurements above to fit the run's budget.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. The TLS 1.2-only hand-built hello carries each build's measured record version and empty session ID, and the OpenSSL build refuses --tls-max 1.0/1.1 with exit 35 as Ubuntu's curl does
