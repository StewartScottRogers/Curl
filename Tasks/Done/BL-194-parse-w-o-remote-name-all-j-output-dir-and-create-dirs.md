---
id: BL-194
title: Parse -w, -O, --remote-name-all, -J, --output-dir and --create-dirs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-194 — Parse -w, -O, --remote-name-all, -J, --output-dir and --create-dirs

## Goal

`-w`/`--write-out` (including `@file` and `@-`), `-O`/`--remote-name`, `--remote-name-all`, `-J`/`--remote-header-name`, `--output-dir` and `--create-dirs` parse into `CommandLineOptions`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C8. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `-o` pairing with URLs exists; `-O` pairs the same way (BL-078 warns on extra outputs).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] `-w @file` and `-w @-` read the template through the injected readers.
- [x] `-O` pairs with URLs as `-o` does; `--remote-name-all` applies to every URL.
- [x] Every refusal and warning this option group can raise exits 2 with the exact lines measured on curl 8.21.0, and every `--no-` spelling curl accepts for these options is measured and tested (a test per spelling).
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C8 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0027 (the URL/output pairing model); no task in Doing named it.
- Measured with `/mingw64/bin/curl` 8.21.0 on 2026-09-26. Parsing: `curl <args> --bogus` (accepted options reach the unknown-option refusal). Pairing: `curl -s <args>` in an empty directory against `file:///C:/Windows/win.ini` (no loopback server was needed; a file URL shows where each body goes). Results are pinned in the summaries of `CommandLineRemoteNameOptionTests` and `CommandLineWriteOutOptionTests` and tabled in ADR-0027. Highlights:
  - `--no-remote-name`, `--no-remote-name-all`, `--no-remote-header-name`, `--no-create-dirs` (and `=x`) accepted; `--no-write-out`, `--no-output-dir` (and `=x`) exit 2 "cannot be reversed with a --no- prefix".
  - `--output-dir ''` / `--output-dir=` exit 2 "blank argument where content is expected"; `--output-dir -x` and `-w -x` do not warn; `-w ''` is accepted.
  - `-w @nonexist` exits 26 with `curl: Failed to open nonexist` + `curl: option -w: error encountered when reading a file`; `-s` drops the first line. `-w @empty.txt` warns `Warning: Failed to read empty.txt` (`<stdin>` for empty `@-`) and clears the template; a file of only CR LF or NUL does not warn. File text loses every CR, LF and NUL (`x%{http_code}
` prints `x000`).
  - `-O -O u` warns `Warning: Got more output options than URLs` after the transfer; `--no-remote-name --no-remote-name u` does not (dropped when no entry is free and `--remote-name-all` is off).
- Decision (ADR-0027, decided by Claude under Stewart's delegation): `CommandLineOptions.UrlOutputs` mirrors curl's node list; `OutputFiles` became positional (`string?`, trailing nulls trimmed) so `Curl.Console`'s `OutputFiles[index]` pairing stays right with `-O` in the mix (`-O -o x u1 u2` gives `x` to `u2`). Found by the code-reviewer stage; without it the console would have written `u1` to `x`.
- Default taken: the `-w @file` template is decoded as UTF-8 (lossy for other bytes); curl keeps bytes. Revisit in the write-out expansion tasks if it matters.
- Not parse-time and left to BL-239: `Warning: No remote filename, uses "curl_response"` (measured for `-O http://127.0.0.1:1/`).
- Gates: `dotnet build -warnaserror` clean; fast tests green (Curl.Cli.UnitTests 1246 passed); `Measure-CodeQuality.ps1` reports Curl.Cli.UnitLibrary 100% line / 100% branch, 0 failing, worst CRAP 10, and Curl.Console unchanged at 100/100. The 10 failing members in Curl.Networking and Curl.Protocol.File predate this task.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -w (incl. @file, @-), -O, --remote-name-all, -J, --output-dir and --create-dirs parse into CommandLineOptions with curl 8.21.0's pairing, refusals and warnings
