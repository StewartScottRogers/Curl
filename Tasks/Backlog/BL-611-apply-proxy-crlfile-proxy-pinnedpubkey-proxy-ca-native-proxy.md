---
id: BL-611
title: Apply --proxy-crlfile, --proxy-pinnedpubkey, --proxy-ca-native, --proxy-ssl-auto-client-cert and --proxy-ssl-allow-beast to the HTTPS proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-490, BL-605, BL-608, BL-609, BL-610]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-611 — Apply --proxy-crlfile, --proxy-pinnedpubkey, --proxy-ca-native, --proxy-ssl-auto-client-cert and --proxy-ssl-allow-beast to the HTTPS proxy

## Goal

The five proxy options act on the HTTPS proxy's handshake exactly as their origin counterparts (BL-490, BL-608, BL-609, BL-610) act on the origin's, with the same exit codes and messages.

## Context

- Conformance audit 2026-09-28, row 15 (Major). Options: BL-605. The origin behaviour lands in BL-490 (`--ca-native`, `--ssl-allow-beast`), BL-608 (pins), BL-609 (CRL) and BL-610 (auto client cert); reuse it with the proxy's `TlsClientOptions` (ADR-0095).

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Tls` as the proxy: a wrong `--proxy-pinnedpubkey`, a missing `--proxy-crlfile`, `--proxy-ca-native`; stderr and exit code copied into Notes.
- [ ] Tests show each proxy option reaching only the proxy's handshake, and the measured failures.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
