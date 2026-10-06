---
id: BL-188
title: Parse the --data-* options, --json, -G and --url-query into CommandLineOptions
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-188 — Parse the --data-* options, --json, -G and --url-query into CommandLineOptions

## Goal

`--data-binary`, `--data-raw`, `--data-ascii`, `--data-urlencode`, `--json`, `-G`/`--get` and `--url-query` parse into `CommandLineOptions` with curl's encoding and joining.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C2. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `-d` and joining several `-d` with `&` exist (BL-038, BL-057, BL-080).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Every `--data-urlencode` form (`content`, `=content`, `name=content`, `@file`, `name@file`) encodes as measured on curl 8.21.0.
- [x] `-G` moves the data into the query string (as measured), and `--url-query` appends as measured.
- [x] How several `--json` values join is measured and pinned.
- [x] Every refusal and warning this option group can raise exits 2 with the exact lines measured on curl 8.21.0, and every `--no-` spelling curl accepts for these options is measured and tested (a test per spelling).
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C2 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

- Delivered in Curl.Cli only: rows for `--data-ascii`, `--data-binary`, `--data-raw`, `--data-urlencode`, `--json`, `-G`/`--get` and `--url-query`; new `CommandLineOptions` members `SendsJson`, `DataInQuery`, `UrlQuery`; new `UrlEncodedContent` (curl_easy_escape plus space as `+`) and `QueryUrl.Append(url, options)` (pure string work like `UploadUrl`, ADR-0004). Wiring `QueryUrl`, `SendsJson` headers and the GET method into the transfer is the console/HTTP layer's job, already filed as BL-231 (which depends on this task); no new task filed.
- Measured 2026-09-26 with `/mingw64/bin/curl` 8.21.0 against a Python loopback recorder on 127.0.0.1:18188 (`curl -sS http://127.0.0.1:18188/p <arguments>`, reading the request line and body; `f.txt` = `a b
c
`). Every command and its bytes are written in the summaries of `CommandLinePostDataOptionTests` and `QueryUrlTests` and pinned one test row each. Highlights: `--data-urlencode` splits at the first `=`, else the first `@` (`a=b@c` -> `a=b%40c`, `f.txt@f.txt=x` -> verbatim); `n@empty-file` contributes an empty piece with no name; `--json` pieces join with no separator (`-d x --json y` -> `xy`, `--json y -d x` -> `y&x`) and `--json @file` keeps CR LF; `-G` with data (even empty) ignores every `--url-query` (`--url-query a -G -d b` -> `?b`), `-G` without data uses them; `--url-query` values join with `&` even when empty (`'' + n@f` -> `?&n=...`).
- Decision (default taken): non-ASCII argument text is encoded as UTF-8 (`é` -> `%C3%A9`). The mingw reference sent `%E9` (its ANSI code-page argv); ADR-0004 already chose UTF-8 to match the official Windows build and Linux/macOS, and `-d` already sends UTF-8, so this follows it.
- Refusals: every `@file` that cannot be read exits 26 (not 2) with `curl: Failed to open <file>`, `curl: option <as typed>: error encountered when reading a file` and the try-help line (first line hidden after `-s`), measured for all six value options; each option as the last argument exits 2 `requires parameter`. No option in this group raised a warning in measurement (flag-like values `--x`, `-y`, `-z` were accepted silently).
- `--no-` spellings measured: `--no-get` (and `--no-get=x`) accepted, last spelling wins; `--no-data-ascii`, `--no-data-binary`, `--no-data-raw`, `--no-data-urlencode`, `--no-json`, `--no-url-query` (and each `=x`) exit 2 "cannot be reversed"; one test per spelling in `CommandLineNegationTests`.
- Gates: `dotnet build Curl.Cli.UnitLibrary -warnaserror` clean; fast tests all green (Curl.Cli 813); `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 100% line, 100% branch, 291 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. --data-ascii/-binary/-raw/-urlencode, --json, -G and --url-query parse into CommandLineOptions with curl 8.21.0's encoding and joining; QueryUrl builds the -G/--url-query URL
