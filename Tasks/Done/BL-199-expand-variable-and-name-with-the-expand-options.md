---
id: BL-199
title: Expand --variable and {{name}} with the --expand- options
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-197]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Documentation]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-199 — Expand --variable and {{name}} with the --expand- options

## Goal

`--variable` defines variables and `--expand-<option>` expands `{{name}}` with curl's functions.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C13. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- https://curl.se/docs/manpage.html#--variable (curl 8.21.0).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Every function (`trim`, `json`, `url`, `b64`, `64dec`) and every error case is measured on curl 8.21.0 and pinned.
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C13 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- 2026-09-27 (lane 1, resumed run): the cut-off run had left nothing in the tree, so the work started from the task file.
- Measured with curl 8.21.0 (`/mingw64/bin/curl`, i.e. `C:\Program Files\Git\mingw64in\curl.exe`) against a node loopback server on 127.0.0.1:18199 that echoes the request body, e.g. `curl --no-progress-meter --variable a=hello --expand-data '[{{a}}]' http://127.0.0.1:18199/` -> `[hello]`. Every measured case, its bytes and its stderr lines are recorded in the doc comments of `CommandLineVariableOptionTests` and `CommandLineExpandOptionTests` and pinned there; the rules were checked against `src/var.c`, `src/tool_getparam.c`, `src/tool_paramhlp.c` and `lib/curlx/base64.c` at tag `curl-8_21_0`.
- Git Bash rewrites some arguments before curl sees them (`%NAME[1-2]=zzzz` came out as if the environment variable were unset); the environment-import cases were measured again from PowerShell, where they match the source. Measure `--variable` from PowerShell.
- Behaviour worth knowing: `--expand-<flag>` is refused as `variable expansion failure` whenever there is a next argument or an attached value, because curl looks at `nextarg` before checking the option type; `\{{` is only unescaped when some other reference in the value was replaced; an imported environment value is never cut to a byte range.
- Decision (ADR-0064, decided by Claude under Stewart's delegation): variables hold bytes and expand through UTF-8 option values, so non-UTF-8 bytes expanded unencoded become U+FFFD; `%name` reads the process environment; `Failed to open <file>: <reason>` uses `Invalid argument` / `Permission denied` / `No such file or directory` as the Windows curl does.
- Added `Documentation` to `touches` for ADR-0064 and its index row; no task in Doing names it.
- Result: `dotnet build` clean, fast tests green (Curl.Cli.UnitTests 1829 passed), `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` 100% line, 100% branch, 0 failing members (worst CRAP 10).

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. --variable (=, @file, @-, %env, byte ranges) and --expand-<option> with trim, json, url, b64 and 64dec work as curl 8.21.0, pinned by measurement
