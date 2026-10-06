---
id: BL-493
title: Parse --skip-existing and skip a transfer whose output file already exists
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-493 — Parse --skip-existing and skip a transfer whose output file already exists

## Goal

With `--skip-existing`, a transfer whose `-o`/`-O` file already exists is not performed at all (no connection is made) and the run carries on to the next URL, printing and exiting as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 11 (Blocker, S).
- Alias-table row `skip-existing` (`Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`); no row in `CommandLineOptionTable.cs`.
- The per-URL loop is in `Curl.Console/CurlCommandRunner.cs`; output files go through `IOutputPaths`.
- The manual (`CurlManual.txt`, `--skip-existing`) says the transfer is skipped when the file exists; the note curl prints (and whether only under `-v`) must be measured.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` (`-o out.txt --skip-existing`, with and without `-v`, file present and absent, and two URLs where only the first file exists): stdout, stderr, exit code and whether a connection was accepted, copied into Notes.
- [x] `--skip-existing` parses; `Curl.Cli.UnitTests` covers it.
- [x] A `Curl.Console.UnitTests` test shows no handler call and no connection for an existing file, the measured stderr and exit code, and the second URL still transferred.
- [x] New tests are platform-neutral.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli.UnitLibrary` and `Curl.Console`.

## Notes

Measured 2026-09-28 with curl 8.21.0 (Windows, Schannel), `Record-CurlExchange.ps1` answering
`HTTP/1.1 200 OK`, `Content-Length: 5`, `hello`, absolute `-o` paths (the recorder runs curl in
the process's working directory, not PowerShell's location, so relative names land elsewhere):

| Case | Exit | Connection | stdout | stderr |
| --- | ---: | --- | --- | --- |
| `-o out.txt --skip-existing`, file present | 0 | none | empty | empty (no progress meter) |
| same with `-v` | 0 | none | empty | `Note: skips transfer, "<path>" exists locally`, wrapped at 79 columns as a note (`Note: ` on each piece) |
| same with `-s -v` | 0 | none | empty | the same note (`-s` does not hide it) |
| `-s -o fresh.txt --skip-existing`, file absent | 0 | one | empty | empty; file holds `hello` |
| `-s --skip-existing -o one.txt URL1 -o two.txt URL2`, only one.txt present | 0 | one (`GET /2`) | empty | empty; one.txt untouched, two.txt `hello` |
| present, `-v -w "[%{http_code}\|%{filename_effective}\|%{exitcode}\|%{url}\|%{num_connects}]"` | 0 | none | `[000\|<path>\|0\|http://127.0.0.1:18493/a\|0]` | the note |
| `--skip-existing --no-skip-existing`, file present | 0 | one | empty | empty; overwritten |

Implementation: `--skip-existing` is a `NegatableFlag` row setting `CommandLineOptions.SkipExisting`.
The runner checks the resolved output path (after `--output-dir`, after `--create-dirs` as curl's
`single_transfer` orders it) through a new `IOutputPaths.Exists` (file or directory, as `stat`),
and skips with `TransferResult.Success(0)`, setting `%{filename_effective}` and writing the note
under any `Trace` kind. A body to standard output is never skipped.

Choice (sensible default): the `--create-dirs` and skip checks now run before the proxy is
chosen, not after, because curl checks both in `single_transfer` and the proxy only at perform
time; a skipped transfer therefore never fails on a bad `-x`. No existing test changed.
`-J` is checked against the URL-derived `-O` name only, since the header name is not known
before the transfer. No ADR: every behaviour is measured curl, nothing was decided beyond it.
The review and conformance stages were not run as separate agents; the measured table above is
pinned test for test in `CurlCommandRunnerSkipExistingTests`.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --skip-existing parses and skips a transfer whose -o/-O file exists: no connection, exit 0, curl's -v note, next URL still runs
