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
completed:
---
# BL-1152 — Pin the TLS 1.2-only hand-built hello's session ID and record version, and OpenSSL's --tls-max 1.0 refusal

## Goal

Below a TLS 1.3 ceiling, `HandBuiltTlsProvider`'s first record matches the platform curl's in its record version and legacy session ID too, and the OpenSSL build refuses `--tls-max 1.0` as Ubuntu's curl does.

## Context

- BL-941 / ADR-0340 measured with `Record-CurlExchange.ps1 -Script` (`read`, `close`) against `https://localhost`: Schannel's `--tls-max 1.2` hello is in a record with version `0x0303` (its `--tls-max 1.0` one `0x0301`), and both builds send an empty legacy session ID; BL-941 pinned only the extensions.
- Ubuntu's curl 8.18.0 (OpenSSL 3.5.5) with `--tls-max 1.0` sends a `protocol_version` alert (`15 03 03 00 02 02 46`) and no hello, exit 35. Measure its stderr line before pinning.
- Start at `Tls12ClientHandshake.SessionIdToOffer`, `Tls12ClientConnection`'s record writer and `HandBuiltTlsProviderTests.ClientHello.cs`.

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` show the TLS 1.2-only hello's record version and session ID are the measured ones for each build.
- [ ] A test shows the OpenSSL build with `--tls-max 1.0` fails with curl's measured exit code and stderr line.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Tls.UnitLibrary` and `Curl.Networking.UnitLibrary`.

## Notes

- Filed by BL-941 (ADR-0340).

## Log

- 2026-10-02: Created.
