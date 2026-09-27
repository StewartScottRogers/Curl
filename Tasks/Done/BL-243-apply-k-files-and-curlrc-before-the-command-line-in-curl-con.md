---
id: BL-243
title: Apply -K files and .curlrc before the command line in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-197, BL-198, BL-230]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-243 — Apply -K files and .curlrc before the command line in Curl.Console

## Goal

`Curl.Console` reads `.curlrc` (unless `-q`) and `-K` files with injected readers so their options apply in curl's order.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W14. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-230 added as a dependency beyond the plan.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] A `.curlrc` in a fake home and a `-K` file each change a transfer as measured on curl 8.21.0.
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W14 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- From BL-198 (2026-09-27): `Curl.Cli` now reads `.curlrc`, but only through `CommandLineParser.Parse(arguments, pathExists, passwordPrompt, dataFileReader, DefaultConfigFileSearch.ForProcess)`; `CurlCommandRunner` still calls `Parse(arguments)`, which never reads it. Switch the call, and print `Note: Read config file from '<path>'` from `CommandLineOptions.DefaultConfigFile` when `-v` is on, after the command line is read (curl 8.21.0 `operate`). `-K` files are already applied in place by `Curl.Cli`.
- Done (2026-09-27): `CurlCommandRunner` now parses with `CommandLineParser.Parse(arguments, Path.Exists, ConsolePasswordPrompt.ForProcessConsole, reader, search)`; two new optional constructor parameters, `IDataFileReader? configFileReader` (default `DiskDataFileReader.ForProcess`) and `DefaultConfigFileSearch? defaultConfigFileSearch`. `CurlComposition.CreateRunner` (production) passes `DefaultConfigFileSearch.ForProcess`.
- Choice (sensible default): a runner given no search reads no `.curlrc` (an empty `DefaultConfigFileSearch`), so the existing runner tests and the connector-wired composition never read the real home directory. Only the production composition searches the process's environment.
- Measured on curl 8.21.0 (`/mingw64/bin/curl`, Schannel) with `Record-CurlExchange.ps1`, `CURL_HOME=%TEMP%\bl243` holding `.curlrc` = `-H "X-From-Curlrc: yes"` and `k.txt` = `header = "X-From-K: yes"`:
  - `curl http://127.0.0.1:18243/a -K %TEMP%\bl243\k.txt` sent `GET /a HTTP/1.1`, `Host`, `User-Agent: curl/8.21.0`, `Accept: */*`, `X-From-Curlrc: yes`, `X-From-K: yes`: the `.curlrc` header before the `-K` one.
  - `curl -q http://127.0.0.1:18244/a` sent neither header.
  - `curl -v http://127.0.0.1:18245/a` began stderr with `Note: Read config file from 'C:\Users\Stewart ` / `Note: Rogers\AppData\Local\Temp\bl243\.curlrc'` (wrapped at 79 columns like a warning, `Note: ` on each piece), then the `*` verbose lines.
  - The note also appears under `-s -v`, under `--trace-ascii` alone, after `Warning: -v, --verbose overrides an earlier trace option`, and before `-V` output; not with `-v --no-verbose` and not without `-v` or a trace option.
  - For a refused command line curl prints it too (after the refusal lines for an unknown option read after `-v`; before them for no URL). A refused `CommandLineParseResult` carries no options, so that needs `Curl.Cli`: filed as BL-351.
- Tests: `CurlCommandRunnerConfigFileTests` (13 cases) and `WarningLineWrapperTests.WrapNoteText_MeasuredConfigFileNoteAtDefault79Columns_IsCurlsTwoLines`. `WarningLineWrapper` gained `WrapNoteText`, the same `voutf` rule with the `Note: ` prefix.
- Gates: `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line, 100% branch, 0 failing members (238).

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Curl.Console reads .curlrc (unless -q/--disable) and -K files through injected readers, applied in curl 8.21.0's order, with the -v config-file note
