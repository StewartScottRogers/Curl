---
id: BL-659
title: Negotiate h2 with ALPN and accept --http2 and --http2-prior-knowledge where the HTTP/2 ADR allows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-658, BL-490]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-659 — Negotiate h2 with ALPN and accept --http2 and --http2-prior-knowledge where the HTTP/2 ADR allows

## Goal

On the platforms BL-655's ADR names, the TLS handshake offers `h2,http/1.1` as curl 8.21.0's OpenSSL build does (and respects `--http1.1`, `--http1.0`, `--no-alpn`), the handler uses HTTP/2 when `h2` is selected, and `--http2`/`--http2-prior-knowledge` are accepted instead of refused; on the other platforms nothing changes.

## Context

- Conformance audit 2026-09-28, row 32. If BL-655's ADR decides not to offer HTTP/2, move this task to `Deferred` with that reason.
- ALPN is set in `Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs` (BL-490 adds `--no-alpn`); the refusals of ADR-0017 are in `Curl.Cli.UnitLibrary` (`http2`, `http2-prior-knowledge` rows); the version choice reaches the handler through `HttpVersionPreference` (Abstractions) — if a new value is needed there, file an Abstractions task and depend on it.

## Acceptance criteria

- [ ] Tests pin the ALPN list offered for each version option per platform (`OSCondition`), the handler taking the HTTP/2 path when `h2` is negotiated, and `--http2`/`--http2-prior-knowledge` accepted where the ADR allows and refused as before elsewhere.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
