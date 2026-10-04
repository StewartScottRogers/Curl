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
completed:
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

- [ ] A test with `isWindows: false` shows `--proxy-http2` sets the proxy-HTTP/2 setting and a later `--no-proxy-http2` clears it.
- [ ] A test with `isWindows: true` shows `--proxy-http2` is refused with the ADR-0137 line and exit 2.
- [ ] A test shows `--proxy-http3` is refused with the ADR-0137 line and exit 2 on both platforms.
- [ ] `--ai-help` describes `--proxy-http2` (refused on Windows) and `--proxy-http3` (refused everywhere), and its tests pass.
- [ ] `Curl.Cli.UnitLibrary` keeps 100% line and branch coverage. `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-03: Created.
