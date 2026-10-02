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
completed:
---
# BL-941 — Send the TLS 1.2-only hand-built ClientHello in the platform profile's extension order

## Goal

When the version range's ceiling is below TLS 1.3, `HandBuiltTlsProvider`'s ClientHello follows the platform profile's extension order too, as the platform curl's does, not `Tls12ClientHelloBuilder`'s fixed OpenSSL order (ADR-0235 decision 3).

## Context

- ADR-0235 (BL-820): above that ceiling the hello is the profile's; below it the profile's lists apply but `Tls12ClientSettings` has no extension order, so the Schannel build's `--tls-max 1.2` hello is in OpenSSL's order.
- Measure first: `Record-CurlExchange.ps1 -Tls` with `--tls-max 1.2` (and `--tls-max 1.0`) against the Windows reference build, and under WSL for the OpenSSL build, to see which of the profile's extensions each build drops for a TLS 1.2 ceiling (the TLS 1.3 ones, at least). Pin what is measured.
- Likely shape: an `ExtensionOrder` (and fixed extensions) on `Tls12ClientSettings`, defaulting to today's order, and `ClientHelloProfileMapping` giving it the profile's order without the TLS 1.3-only extensions.

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` capture the hello each build sends with a TLS 1.2 ceiling and show its extension order and lists are the measured ones.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Tls.UnitLibrary` and `Curl.Networking.UnitLibrary`.

## Notes

- Filed by BL-820 (ADR-0235).

## Log

- 2026-09-29: Created.
- 2026-10-02: Backlog -> Doing.
