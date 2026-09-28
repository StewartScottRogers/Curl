---
id: BL-659
title: Negotiate h2 with ALPN and accept --http2 and --http2-prior-knowledge on every platform
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-658, BL-490]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-659 — Negotiate h2 with ALPN and accept --http2 and --http2-prior-knowledge on every platform

## Goal

On Windows, Linux and macOS, the TLS handshake offers the ALPN list BL-655's ADR gives each platform and version option (`h2,http/1.1` where curl's builds offer it; respecting `--http1.1`, `--http1.0`, `--no-alpn`), the handler uses HTTP/2 when `h2` is selected, `--http2-prior-knowledge` speaks HTTP/2 from the first byte over cleartext, and `--http2`/`--http2-prior-knowledge` are accepted instead of refused everywhere.

## Context

- Conformance audit 2026-09-28, row 32. Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): HTTP/2 is offered on every platform (BL-655's ADR supersedes ADR-0017 for HTTP/2). The cleartext `h2c` upgrade for `--http2` over `http://` is BL-716.
- ALPN is set in `Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs` (BL-490 adds `--no-alpn`); the refusals of ADR-0017 are in `Curl.Cli.UnitLibrary` (`http2`, `http2-prior-knowledge` rows); the version choice reaches the handler through `HttpVersionPreference` (Abstractions) — if a new value is needed there, file an Abstractions task and depend on it.

## Acceptance criteria

- [ ] Tests pin the ALPN list offered for each version option (per platform with `OSCondition` only where BL-655's ADR gives platforms different defaults), the handler taking the HTTP/2 path when `h2` is negotiated and with `--http2-prior-knowledge`, and `--http2`/`--http2-prior-knowledge` accepted on every platform.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
