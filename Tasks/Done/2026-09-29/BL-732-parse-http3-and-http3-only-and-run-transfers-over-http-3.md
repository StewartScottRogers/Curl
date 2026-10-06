---
id: BL-732
title: Parse --http3 and --http3-only and run transfers over HTTP/3
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-731, BL-728]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0182-http3-options-parse-as-version-options-and-the-console-composes-a-quic-dialer.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-732 — Parse --http3 and --http3-only and run transfers over HTTP/3

## Goal

`--http3` and `--http3-only` are accepted instead of refused (ADR-0017's exit 2 goes away), and `Curl.Console` runs an `https://` transfer over HTTP/3: `--http3` races QUIC against TCP and falls back to HTTP/2 or HTTP/1.1 as BL-718's ADR records curl doing, `--http3-only` uses QUIC alone and fails with the measured exit when it cannot, on every platform.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Refusal today: the `http3` and `http3-only` rows in `Curl.Cli.UnitLibrary` (ADR-0017, superseded by BL-718's ADR). Composition: `Curl.Console/CurlTransports.cs`, `CurlComposition.cs`; version choice via the new `HttpVersionPreference` values (BL-721). QUIC connector: BL-728; handler path: BL-731.
- `--http3` with an `http://` URL, and combinations with `--http1.1`/`--http2` (last wins or not), follow curl's measured behaviour from BL-718.

## Acceptance criteria

- [x] `Curl.Cli.UnitTests` show both options parsing into the version preference, with the interplay rules as measured.
- [x] `Curl.Console.UnitTests` with fake connectors show an HTTP/3 transfer end to end, `--http3` falling back when the QUIC dial fails, `--http3-only` failing with the measured exit and message, on every platform (no `OSCondition` refusal).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured with curl.se's 8.18.0 ngtcp2 build (WinGet) against `http://127.0.0.1:1/`: `--http1.1 --http3`
  and `--http3 --http3-only` print `Warning: Overrides previous HTTP version option`; `--http3 --http3` does
  not; `--http3=x` is accepted; `--no-http3` is "cannot be reversed". So both options go through
  `SelectHttpVersion` like the other version options. Recorded in ADR-0182 (decided under delegation).
- The handler's QUIC-then-TCP fallback was already in place (BL-731); this task maps the options to
  `HttpVersionPreference.Http3`/`Http3Only`, offers `h2,http/1.1` over TCP under either on every platform,
  composes one `QuicDialer` (origin TLS options, no local bind, as TCP has none yet) into `TcpConnector`, and
  makes `EndPointRecordingConnector` forward `ConnectMultiplexedAsync` (it would otherwise hit the interface
  default, "QUIC is not available on this connector").
- `CommandLineOption.UnsupportedFlag` had no row left and was removed with its tests.
- Added the new ADR file and the ADR index to `touches`: no task in Doing names either.
- Not here, already filed: the timed race against TCP (`--happy-eyeballs-timeout-ms`) is BL-835; `-v`,
  `%{http_version}` and `-V HTTP3` output is BL-734.
- Measured gates: Cli.UnitLibrary 100/100 (761 members, 0 failing), Console 100/100 (603 members, 0
  failing); Cli.UnitTests 2837 passed, Console.UnitTests 1516+ passed, full fast run green.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --http3 and --http3-only parse as version options and run HTTP/3 transfers through the composed QuicDialer, falling back to TCP under --http3
