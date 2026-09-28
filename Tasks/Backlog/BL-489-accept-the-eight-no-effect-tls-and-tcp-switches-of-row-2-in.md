---
id: BL-489
title: Parse --tcp-nodelay, --alpn, --sessionid, --keepalive, --styled-output, --ssl-allow-beast, --ca-native and --ssl-revoke-best-effort
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-489 — Parse --tcp-nodelay, --alpn, --sessionid, --keepalive, --styled-output, --ssl-allow-beast, --ca-native and --ssl-revoke-best-effort

## Goal

The eight switches `--[no-]tcp-nodelay`, `--[no-]alpn`, `--[no-]sessionid`, `--[no-]keepalive`, `--[no-]styled-output`, `--ssl-allow-beast`, `--ca-native` and `--ssl-revoke-best-effort` parse into `CommandLineOptions` and print exactly what curl 8.21.0 prints (if anything), instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 2 (Blocker): curl 8.21.0 exits 0 for each; only the exit code under `-s` was measured, so the standard-error text (without `-s`, and under `-v`) is not yet known.
- Each is a row in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs` with its `--no-` rule (`alpn`, `keepalive`, `sessionid` are `Documented`, `tcp-nodelay` and `styled-output` `Accepted`); none has a row in `CommandLineOptionTable.cs`.
- This task parses and stores them. Acting on them in the connector is BL-490; `--styled-output` (bold header names on a terminal) is stored only, because redirected output is never styled and the tests never run on a terminal.
- Defaults to store: TCP_NODELAY on, ALPN on, session ID cache on, keepalive on, styled output on (curl's documented defaults in `Curl.Cli.UnitLibrary/CurlManual.txt`).

## Acceptance criteria

- [ ] Measured first: the reference curl 8.21.0 run through `Record-CurlExchange.ps1` against a loopback 200 (and, for the TLS switches, `-Tls -k` https) for each switch and each `--no-` form, with and without `-s` and with `-v`, and stdout, stderr and exit code copied into Notes before any text is pinned.
- [ ] Each switch and accepted `--no-` form sets a named, documented property on `CommandLineOptions`; `Curl.Cli.UnitTests` covers every spelling with data rows, and a `--no-` form curl refuses is refused with its measured text.
- [ ] A `Curl.Console.UnitTests` test pins the measured standard-error bytes and exit code for at least `--tcp-nodelay`, `--no-alpn` and `--ca-native` through the runner with a fake handler.
- [ ] New tests are platform-neutral; where the Schannel and OpenSSL builds differ (for instance `--ca-native` or `--ssl-revoke-best-effort` off Windows), each answer is pinned in its own `OSCondition` test.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
