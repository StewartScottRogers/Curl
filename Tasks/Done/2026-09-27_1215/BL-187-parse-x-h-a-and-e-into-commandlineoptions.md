---
id: BL-187
title: Parse -X, -H, -A and -e into CommandLineOptions
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-187 — Parse -X, -H, -A and -e into CommandLineOptions

## Goal

`-X`/`--request`, `-H`/`--header` (including `@file`), `-A`/`--user-agent` and `-e`/`--referer` are rows in `CommandLineOptionTable` and parse into `CommandLineOptions`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C1. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Pattern: existing rows in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` and `IDataFileReader` for `@file` (BL-080).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Several `-H` values are kept verbatim in order; `-H @file` reads one header per line through `IDataFileReader`.
- [x] `-e ';auto'` is kept as given.
- [x] Every refusal and warning this option group can raise exits 2 with the exact lines measured on curl 8.21.0, and every `--no-` spelling curl accepts for these options is measured and tested (a test per spelling).
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Plan (delivered in-session, 2026-09-26): four rows in `CommandLineOptionTable`: `-X`/`--request` as `Text` (blank refused), `-H`/`--header` as `Value` with `AddHeaders`, `-A`/`--user-agent` and `-e`/`--referer` via `AcceptingEmpty`; new `CommandLineOptions` members `RequestMethod`, `Headers`, `UserAgent`, `Referer`; new `CommandLineWarning.HeaderDoesNotLookLikeAHeader`; `-d @file` and `-H @file` now share one `ReadAtFile` helper and one refusal (`DataFileUnreadable`). Tests: `Curl.Cli.UnitTests/CommandLineHttpRequestOptionTests.cs`.
- Measured with `/mingw64/bin/curl` 8.21.0 (Schannel) in Git Bash on 2026-09-26, against `http://127.0.0.1:1/` for stderr and exit codes, and a Python loopback listener on 127.0.0.1:18787 printing the raw request (BL-165's `Record-CurlExchange.ps1` does not exist yet):
  - `curl -X '' URL` and `--request ''`: `curl: option -X: blank argument where content is expected` + try-help, exit 2.
  - `curl -H foo URL`: `Warning: The provided HTTP header 'foo' does not look like a header?`, then the transfer. Same for `'foo bar'`, `-x`, `''`; no warning for `'foo;'`, `':'`, `'X: y'`. `-s -H foo` and `-s -S -H foo` drop it; `-H foo -s` keeps it.
  - `curl -H @h1.txt URL` with h1.txt = `A: 1\r\nB: 2\n\n\r\n  C: 3\nnocolon\nD;\n\nE: 4` (escapes shown) sent `A: 1`, `B: 2`, `  C: 3`, `D:`, `E: 4` (libcurl itself drops `nocolon` and turns `D;` into `D:`; the tool keeps every non-empty line, split on runs of CR/LF, and warns about none). `-H @empty.txt` adds nothing; `-H @- < in.txt` reads stdin. `-H 'X: 1' -H 'X: 2'` sends both in order.
  - `curl -H @nosuch URL`: `curl: Failed to open nosuch`, `curl: option -H: error encountered when reading a file`, try-help, **exit 26** (not 2); `--header=@nosuch` and `-H@nosuch` name the option as typed; `-s` first drops line one, `-s -S` keeps it; `-H @` prints `curl: Failed to open ` with a trailing space. Identical to `-d @file`, so `CommandLineRefusal.DataFileUnreadable` is reused.
  - `curl -A '' -e '' URL` accepted, request has no `User-Agent` header; `-X PUT -A agent -e ';auto'` accepted. `-A -x`, `-e -x`, `-X -x` take `-x` with no warning.
  - `--no-request`, `--no-header`, `--no-header=x`, `--no-user-agent`, `--no-referer`: `curl: option <as typed>: the given option cannot be reversed with a --no- prefix` + try-help, exit 2.
- Decision (sensible default): criterion 3 says every refusal exits 2, but curl 8.21.0 exits 26 for an unreadable `-H @file`; the measurement wins, as the task's own measure-first rule requires, and the test pins 26.
- Quality: `Measure-CodeQuality.ps1 -SkipTestRun -Library 'Curl.Cli*'` after a coverage run: Curl.Cli.UnitLibrary 100% line, 100% branch, 269 members, 0 failing, worst CRAP 10. The coverage run itself exited non-zero because `Curl.Networking.UnitTests` `AuthenticateAsClientAsync_WithoutClientCertificate_PresentsNone` failed twice under the coverage collector; it passes in the plain fast run (`dotnet test --filter "TestCategory!=Integration"`, all green) and is outside this task's `touches`.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -X/--request, -H/--header (with @file and @-), -A/--user-agent and -e/--referer parse into CommandLineOptions with curl 8.21.0's measured warnings and refusals
