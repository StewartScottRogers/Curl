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
completed: 2026-09-28
---
# BL-659 — Negotiate h2 with ALPN and accept --http2 and --http2-prior-knowledge on every platform

## Goal

On Windows, Linux and macOS, the TLS handshake offers the ALPN list BL-655's ADR gives each platform and version option (`h2,http/1.1` where curl's builds offer it; respecting `--http1.1`, `--http1.0`, `--no-alpn`), the handler uses HTTP/2 when `h2` is selected, `--http2-prior-knowledge` speaks HTTP/2 from the first byte over cleartext, and `--http2`/`--http2-prior-knowledge` are accepted instead of refused everywhere.

## Context

- Conformance audit 2026-09-28, row 32. Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): HTTP/2 is offered on every platform (BL-655's ADR supersedes ADR-0017 for HTTP/2). The cleartext `h2c` upgrade for `--http2` over `http://` is BL-716.
- ALPN is set in `Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs` (BL-490 adds `--no-alpn`); the refusals of ADR-0017 are in `Curl.Cli.UnitLibrary` (`http2`, `http2-prior-knowledge` rows); the version choice reaches the handler through `HttpVersionPreference` (Abstractions) — if a new value is needed there, file an Abstractions task and depend on it.

## Acceptance criteria

- [x] Tests pin the ALPN list offered for each version option (per platform with `OSCondition` only where BL-655's ADR gives platforms different defaults), the handler taking the HTTP/2 path when `h2` is negotiated and with `--http2-prior-knowledge`, and `--http2`/`--http2-prior-knowledge` accepted on every platform.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Plan (no new ADR: ADR-0141 already decides every behaviour here). `Curl.Cli.UnitLibrary` records the last version option as the new `RequestedHttpVersion` (`Http10`, `Http11`, `Http2`, `Http2PriorKnowledge`; `null` = none given) in `CommandLineOptions.HttpVersion`, replacing `HttpVersionPreference?`, because the Abstractions enum has no `--http2` value and the default must stay distinguishable from an explicit `--http1.1` (they offer different ALPN off Windows). No Abstractions change was needed, so no Abstractions task was filed.
- `--http2` and `--http2-prior-knowledge` are plain flags now: accepted on every platform, going through the same "Overrides previous HTTP version option" warning as `-0`/`--http1.1`; `--no-http2*` stays refused as not reversible. `--http3`/`--http3-only` keep ADR-0017's refusal; the Cli refusal tests and the config-file wrap test moved to `http3` (same text, same wrap).
- `Curl.Networking.UnitLibrary`: `HttpApplicationProtocols` holds the three measured lists (`Http11Only`, `H2ThenHttp11`, `H2Only`); `TcpConnector` takes the list for HTTP over TLS to the origin as a new optional constructor argument (default `Http11Only`, so every existing caller and test is unchanged).
- `Curl.Console`: `HttpVersionMapping` maps the option to the handler's `HttpVersionPreference` (`--http2` → `Http11` request line, HTTP/2 only if ALPN picks `h2`, which the handler already honours since BL-658; `--http2-prior-knowledge` → `Http2PriorKnowledge`) and to the ALPN list: `-0`/`--http1.1` `http/1.1`; `--http2` `h2,http/1.1`; `--http2-prior-knowledge` `h2`; none: `http/1.1` on Windows, `h2,http/1.1` elsewhere. `--no-alpn` still empties the offer in the TLS providers.
- The handler taking the HTTP/2 path on a negotiated `h2` and under prior knowledge is pinned by BL-658's tests in `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Http2.cs` (outside this task's touches, unchanged).
- `-V` now lists `HTTP2` under `Features:` on every platform (ADR-0141, Decision 5).
- Left to their own tasks: `--http2` over `http://` sends a plain HTTP/1.1 request with no `Upgrade: h2c` until BL-716; the `-v`/`-i`/`%{http_version}` HTTP/2 lines are BL-660.
- Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Cli 2620+, Networking 1239+, Console 1454); `Measure-CodeQuality.ps1` 100% line and branch, 0 failing members for Curl.Cli.UnitLibrary, Curl.Networking.UnitLibrary and Curl.Console.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --http2 and --http2-prior-knowledge are accepted everywhere, ALPN offers the per-platform list ADR-0141 measured, and -V lists HTTP2
