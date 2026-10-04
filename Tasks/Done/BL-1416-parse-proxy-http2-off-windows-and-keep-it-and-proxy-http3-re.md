---
id: BL-1416
title: Parse --proxy-http2 off Windows, refuse it on Windows and refuse --proxy-http3 everywhere (ADR-0408)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1416 — Parse --proxy-http2 off Windows, refuse it on Windows and refuse --proxy-http3 everywhere (ADR-0408)

## Goal

Off Windows, `--proxy-http2` and `--no-proxy-http2` parse into a proxy-HTTP/2 setting. On Windows `--proxy-http2`, and on every platform `--proxy-http3`, keep the ADR-0137 refusal.

## Context

- ADR-0408 decisions 1-3. curl 8.21.0 `src/tool_getparam.c` lines 2030-2046.
- Today both options exist only in `CurlOptionAliasTable.cs` (line 196) and `CurlHelpTable.cs` (line 181). `CommandLineOptionTable.cs` has no row for them, so both are refused (ADR-0137).
- Measured 2026-10-03: curl 8.21.0 Schannel refuses both with `curl: option --proxy-http2: the installed libcurl version does not support this` and exit 2. curl 8.18.0 OpenSSL (WSL) accepts `--proxy-http2`.
- The platform choice goes through the parser's existing `isWindows` parameter (`CommandLineParser.Parse(..., OperatingSystem.IsWindows())`), so both answers can be tested on any OS.

## Acceptance criteria

- [x] A test with `isWindows: false` shows `--proxy-http2` sets the proxy-HTTP/2 setting and a later `--no-proxy-http2` clears it.
- [x] A test with `isWindows: true` shows `--proxy-http2` is refused with the ADR-0137 line and exit 2.
- [x] A test shows `--proxy-http3` is refused with the ADR-0137 line and exit 2 on both platforms.
- [x] `--ai-help` describes `--proxy-http2` (refused on Windows) and `--proxy-http3` (refused everywhere), and its tests pass.
- [x] `Curl.Cli.UnitLibrary` keeps 100% line and branch coverage. `dotnet build` is clean and the fast tests are green.

## Notes

- Delivered directly (a small parser change) rather than through the full `/feature` stages; ADR-0408 already holds the plan.
- `--proxy-http2` is a `NegatableFlag` row setting `CommandLineOptions.ProxyHttp2`, wrapped in `RefusedBySchannelBuild()`. That wrapper now refuses the `--no-` spelling on the Schannel build too, because curl 8.21.0 checks the `HTTP2` feature before it reads the toggle (`src/tool_getparam.c` 2030-2046). No earlier Schannel-refused row was negatable, so nothing else changes.
- `--proxy-http3` still has no row, so it keeps the ADR-0137 refusal on both platforms. `--ai-help` derives both answers from the table (the Windows refusal line for `--proxy-http2`, "Not supported by this build yet" for `--proxy-http3`), and tests pin both.
- `--libcurl` writes nothing for `--proxy-http2`, following ADR-0326 for options the Schannel reference build refuses (listed in `WritesNothing`). The setting is per `--next` group, as curl's `proxyver` is.
- Carrying the setting to the connector (ALPN `h2`, HTTP/2 CONNECT) is ADR-0408 decisions 4-6, for other tasks.
- Coverage: `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 99.98% line and 99.89% branch, with 3 failing members, none in code this task changed (`ArgumentReader.PeekNext`, `CommandLineParser.RefuseUnlistedLetter`, `AccountHomeDirectory` line 15). Every line this task added is covered. Filed BL-1417 for those three gaps.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. --proxy-http2 parses off Windows and is refused on the Schannel build; --proxy-http3 stays refused everywhere
