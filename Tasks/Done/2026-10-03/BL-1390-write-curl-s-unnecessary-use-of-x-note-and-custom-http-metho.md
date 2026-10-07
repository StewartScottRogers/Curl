---
id: BL-1390
title: Write curl's 'Unnecessary use of -X' note and 'custom HTTP method to HEAD' warning before each transfer whose -X repeats or overrides the inferred method
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: FR-054
created: 2026-10-03
completed: 2026-10-03
---
# BL-1390 — Write curl's 'Unnecessary use of -X' note and 'custom HTTP method to HEAD' warning before each transfer whose -X repeats or overrides the inferred method

## Goal

Before each transfer, Curl writes curl 8.21.0's `customrequest_helper` lines to standard error: `Note: Unnecessary use of -X or --request, <METHOD> is already inferred.` under `-v` when the `-X` method equals, ignoring case, the one the other options already imply; otherwise, when `-X` names `HEAD` in any case and `-s` was not given, the two-line `Warning: Setting custom HTTP method to HEAD with -X/--request may not work the ` / `Warning: way you want. Consider using -I/--head instead.`

## Context

- Today neither text exists anywhere in the solution (`git grep "Unnecessary use of -X"` and `git grep "Setting custom HTTP method"` find nothing); `-X GET` and `-X HEAD` transfers print nothing extra.
- curl 8.21.0 (tag `curl-8_21_0`): `src/tool_helpers.c` lines 101-125 (`customrequest_helper`) compares the `-X` method with `curl_strequal` (case-insensitive) against the default of the inferred request kind - `GET` for none or `-G`, `HEAD` for `-I`, `POST` for `-d`/`--data*`/`--json` and for `-F`, `PUT` for `-T` - and writes `notef("Unnecessary use of -X or --request, %s is already inferred.", dflt[req])` (the default's own spelling, upper case), else, when the method is `head` in any case, `warnf("Setting custom HTTP method to HEAD with -X/--request may not work the way you want. Consider using -I/--head instead.")`. It is called from `src/config2setopts.c` line 989, once per transfer. `notef` writes only under `-v` (also under `-v -s`); `warnf` writes unless `-s`. Both wrap with `WarningLineWrapper`'s rules (`Curl.Console/WarningLineWrapper.cs`, `WrapNoteText` and `WrapText`).
- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1`:
  - `-v -X GET -o NUL http://127.0.0.1:PORT/a`: first stderr line `Note: Unnecessary use of -X or --request, GET is already inferred.`, before the progress meter and `*   Trying`.
  - `-v -X post -d x`: `Note: Unnecessary use of -X or --request, POST is already inferred.`; `-v -X PUT -T file`: `... PUT is already inferred.`; `-v -X GET ftp://127.0.0.1:PORT/a`: the GET note too (not only for HTTP).
  - `--no-progress-meter -o NUL -X HEAD -m 2 http://...`: `Warning: Setting custom HTTP method to HEAD with -X/--request may not work the ` then `Warning: way you want. Consider using -I/--head instead.`; with `-s` nothing.
  - Without `-v`, `-X POST -d x`, `-X GET -I`, `-X get`, `-X POST` alone and `-X GET -d x -G` print nothing.
  - `-v -s -X GET URL1 URL2`: the note is written again before the second transfer, after the first's `left intact` line and before `* Reusing existing http: connection`; `-X HEAD URL1 URL2` writes the two warning lines before each transfer.

## Acceptance criteria

- [x] Tests in `Curl.Console.UnitTests` run `CurlCommandRunner` over fake connectors and pin, as whole stderr lines: the GET note under `-v -X GET`; the POST note under `-v -X post -d x`; the PUT note under `-v -X PUT -T <file>`; the HEAD note under `-v -X HEAD -I`; the GET note for an `ftp://` URL.
- [x] Tests pin the two wrapped HEAD warning lines for `-X HEAD` and `-X head` without `-s`, and nothing with `-s`.
- [x] Tests pin no note without `-v`, no note for `-X GET -d x` (inferred POST), and the note or warning repeated before the second of two URLs, in the measured position.
- [x] `dotnet build Curl.Console.UnitTests -warnaserror` is clean; `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` reports no failing member in the code this task changed.

## Notes

- No option changes, so `--ai-help` is unaffected.
- Test paths for `-T` must be platform-neutral (a temporary file from `Path.GetTempPath()`).
- The `-T` test uses the in-memory file system with a relative `up.txt`, which is platform-neutral without touching the disk, so no `Path.GetTempPath()` file is needed.
- Inferred method, in curl's precedence: `-I` HEAD; `-F` POST; `-d`/`--json` without `-G` POST; `-T` PUT; else GET (`--no-head` and `-G` included). Implemented as `CurlCommandRunner.WriteCustomRequestLinesAsync`/`InferredRequestMethod`, called from `TransferAsync` after the existing per-transfer warning lines, so it repeats before every transfer. The note is gated on `Trace != None` (`-v` or a `--trace` option, as `notef` and the other notes are); the HEAD warning on `!Silent`.
- Measure-CodeQuality -Library Curl.Console: 0 failing members. Curl.Console.UnitTests: 2537 passed, 24 skipped.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Curl writes curl's 'Unnecessary use of -X' note under -v and the -X HEAD warning before each transfer
