---
id: BL-941
title: Send the TLS 1.2-only hand-built ClientHello in the platform profile's extension order
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-10-02
---
# BL-941 — Send the TLS 1.2-only hand-built ClientHello in the platform profile's extension order

## Goal

When the version range's ceiling is below TLS 1.3, `HandBuiltTlsProvider`'s ClientHello follows the platform profile's extension order too, as the platform curl's does, not `Tls12ClientHelloBuilder`'s fixed OpenSSL order (ADR-0235 decision 3).

## Context

- ADR-0235 (BL-820): above that ceiling the hello is the profile's; below it the profile's lists apply but `Tls12ClientSettings` has no extension order, so the Schannel build's `--tls-max 1.2` hello is in OpenSSL's order.
- Measure first: `Record-CurlExchange.ps1 -Tls` with `--tls-max 1.2` (and `--tls-max 1.0`) against the Windows reference build, and under WSL for the OpenSSL build, to see which of the profile's extensions each build drops for a TLS 1.2 ceiling (the TLS 1.3 ones, at least). Pin what is measured.
- Likely shape: an `ExtensionOrder` (and fixed extensions) on `Tls12ClientSettings`, defaulting to today's order, and `ClientHelloProfileMapping` giving it the profile's order without the TLS 1.3-only extensions.

## Acceptance criteria

- [x] `Curl.Networking.UnitTests` capture the hello each build sends with a TLS 1.2 ceiling and show its extension order and lists are the measured ones.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Tls.UnitLibrary` and `Curl.Networking.UnitLibrary`.

## Notes

- Filed by BL-820 (ADR-0235).
- Measured (`Record-CurlExchange.ps1 -Script` with `read` then `close`, `https://localhost`): Schannel 8.21.0 `--tls-max 1.2` sends server_name, status_request, supported_groups, ec_point_formats, signature_algorithms, session_ticket, ALPN, extended_master_secret, renegotiation_info - not the TLS 1.3 profile's order filtered; `--tls-max 1.0` the same without signature_algorithms. Ubuntu's OpenSSL build (WSL, reaching the Windows host address through `--resolve`) sends the profile's order without its TLS 1.3 extensions, ec_point_formats `0,1,2`; its `--tls-max 1.0` sends a protocol_version alert and no hello.
- Decision (ADR-0340): `ClientHelloProfile.Tls12ExtensionOrder` (Schannel's measured; OpenSSL's defaults to `ExtensionOrder`), `Tls12ClientSettings.ExtensionOrder` and `FixedExtensions`; the mapping fixes the profile's ec_point_formats (when an ECDHE group is offered) and status_request, and puts srp after server_name.
- Built directly rather than through the full `/feature` agent chain: a small, measured change in known files, kept inside the run's budget.
- Follow-up filed: BL-1152 (record version, legacy session ID, OpenSSL's `--tls-max 1.0` refusal).

## Log

- 2026-09-29: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. The TLS 1.2-only hand-built hello sends each build's measured extension order and lists
