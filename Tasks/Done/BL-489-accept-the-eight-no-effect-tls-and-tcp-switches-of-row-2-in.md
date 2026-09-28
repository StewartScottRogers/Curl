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
completed: 2026-09-28
---
# BL-489 — Parse --tcp-nodelay, --alpn, --sessionid, --keepalive, --styled-output, --ssl-allow-beast, --ca-native and --ssl-revoke-best-effort

## Goal

The eight switches `--[no-]tcp-nodelay`, `--[no-]alpn`, `--[no-]sessionid`, `--[no-]keepalive`, `--[no-]styled-output`, `--ssl-allow-beast`, `--ca-native` and `--ssl-revoke-best-effort` parse into `CommandLineOptions` and print exactly what curl 8.21.0 prints (if anything), instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 2 (Blocker): curl 8.21.0 exits 0 for each; only the exit code under `-s` was measured, so the standard-error text (without `-s`, and under `-v`) is not yet known.
- Each is a row in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs` with its `--no-` rule (`alpn`, `keepalive`, `sessionid` are `Documented`, `tcp-nodelay` and `styled-output` `Accepted`); none has a row in `CommandLineOptionTable.cs`.
- This task parses and stores them; every one of them then acts (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28: nothing stays parse-only). Acting on them: BL-490 (`--no-alpn`, `--no-keepalive`, `--no-tcp-nodelay`, `--ssl-revoke-best-effort`, `--ca-native`), BL-713 (`--no-sessionid`, `--ssl-allow-beast`), BL-736 (`--styled-output` styles header output on a terminal).
- Defaults to store: TCP_NODELAY on, ALPN on, session ID cache on, keepalive on, styled output on (curl's documented defaults in `Curl.Cli.UnitLibrary/CurlManual.txt`).

## Acceptance criteria

- [x] Measured first: the reference curl 8.21.0 run through `Record-CurlExchange.ps1` against a loopback 200 (and, for the TLS switches, `-Tls -k` https) for each switch and each `--no-` form, with and without `-s` and with `-v`, and stdout, stderr and exit code copied into Notes before any text is pinned.
- [x] Each switch and accepted `--no-` form sets a named, documented property on `CommandLineOptions`; `Curl.Cli.UnitTests` covers every spelling with data rows, and a `--no-` form curl refuses is refused with its measured text.
- [x] A `Curl.Console.UnitTests` test pins the measured standard-error bytes and exit code for at least `--tcp-nodelay`, `--no-alpn` and `--ca-native` through the runner with a fake handler.
- [x] New tests are platform-neutral; where the Schannel and OpenSSL builds differ (for instance `--ca-native` or `--ssl-revoke-best-effort` off Windows), each answer is pinned in its own `OSCondition` test.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-09-28 with `Record-CurlExchange.ps1` against the local curl 8.21.0 (Schannel, x86_64-w64-mingw32): a loopback 200 over http, and over `-Tls -k` https. For each of the 16 spellings (`--tcp-nodelay`, `--no-tcp-nodelay`, `--alpn`, `--no-alpn`, `--sessionid`, `--no-sessionid`, `--keepalive`, `--no-keepalive`, `--styled-output`, `--no-styled-output`, `--ssl-allow-beast`, `--no-ssl-allow-beast`, `--ca-native`, `--no-ca-native`, `--ssl-revoke-best-effort`, `--no-ssl-revoke-best-effort`):
  - with `-s`: exit 0, stdout 0 bytes, stderr empty;
  - without `-s`: exit 0, stdout 0 bytes, stderr only the ordinary progress meter, the same as a run without the switch;
  - with `-v`: exit 0, the ordinary verbose trace; no line names the switch;
  - `-s -v -k` over https: the same trace for every switch, except that `--no-alpn` drops `* ALPN: curl offers http/1.1` and `* ALPN: server did not agree on a protocol. Uses default.` That is the switch acting, BL-490's work.
- No `--no-` form is refused: curl 8.21.0 accepts all eight, so no refusal text is pinned.
- Properties on `CommandLineOptions`, each a `NegatableFlag` row where the last spelling wins: `TcpNoDelay`, `UseAlpn`, `ReuseSessionIds`, `TcpKeepAlive` and `StyledOutput` default to `true`; `AllowBeast`, `UseNativeCaStore` and `RevocationCheckBestEffort` default to `false`. Each is named for what it turns on, like the existing `SkipRevocationCheck` and `FtpUseEprt`. This is naming, not a behaviour decision, so no ADR.
- Platforms: in curl's tool parser these switches only set booleans, whatever the TLS backend, so the Schannel and OpenSSL builds parse them the same way and no `OSCondition` split is needed. What `--ca-native` and `--ssl-revoke-best-effort` do on each backend is BL-490's work.
- Tests: `Curl.Cli.UnitTests/CommandLineConnectionSwitchTests.cs` covers the defaults, all 16 spellings and 8 last-one-wins rows. `Curl.Console.UnitTests/CurlCommandRunnerConnectionSwitchTests.cs` has 10 rows, including `--tcp-nodelay`, `--no-alpn` and `--ca-native`: under `-s` each exits 0, writes nothing to stderr and still dispatches the transfer. Cli 2098 passed, Console 1024 passed. `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 100% line, 100% branch, 0 failing members.
- Solution-wide `dotnet format --verify-no-changes` reports end-of-line errors in `Curl.Output.UnitTests/TraceTransferEventWriterOpenSslTlsTests.cs`, which is outside this task. The four files this task changed are clean.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --[no-]tcp-nodelay, --[no-]alpn, --[no-]sessionid, --[no-]keepalive, --[no-]styled-output, --[no-]ssl-allow-beast, --[no-]ca-native and --[no-]ssl-revoke-best-effort parse into CommandLineOptions and exit 0 silently as curl 8.21.0 does
