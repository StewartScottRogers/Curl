---
id: BL-494
title: Parse --remove-on-error and delete the output file of a failed transfer
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-494 — Parse --remove-on-error and delete the output file of a failed transfer

## Goal

With `--remove-on-error`, a transfer that fails removes the `-o`/`-O` file it wrote, as curl 8.21.0 does; a successful transfer, and standard output, are unaffected.

## Context

- Conformance audit 2026-09-28, row 11 (Blocker, S).
- Alias-table row `remove-on-error`, `--no-` accepted (`Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`); no row in `CommandLineOptionTable.cs`.
- Output files are managed in `Curl.Console` (`OutputFileTarget.cs`, `DeferredOutputFileStream.cs`) behind `IOutputPaths`; the failure point is the transfer result in `CurlCommandRunner.cs`.
- Whether curl also removes the file for `-f` (exit 22) and for a partial transfer (exit 18), and what it does with a file that existed before the transfer, must be measured.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `--remove-on-error -o out.txt` for a 200, a 404 with `-f`, a body cut short (exit 18), and with `out.txt` existing beforehand; files left behind, stderr and exit code copied into Notes.
- [x] `--remove-on-error` and `--no-remove-on-error` parse; `Curl.Cli.UnitTests` covers both.
- [x] `Curl.Console.UnitTests` tests through `IOutputPaths` pin each measured case, including that the exit code and message are those of the failure.
- [x] New tests are platform-neutral.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli.UnitLibrary` and `Curl.Console`.

## Notes

Measured 2026-09-28 with the local curl 8.21.0 (mingw, Schannel) through `Record-CurlExchange.ps1`,
a loopback server, `-o <temp>\out.txt`:

| Case | Exit | stderr (`-s -S` unless noted) | out.txt afterwards |
| --- | ---: | --- | --- |
| 200, 5-byte body | 0 | (none) | kept, `hello` |
| 404 with `-f` | 22 | `curl: (22) The requested URL returned error: 404` | never created |
| 404 with `-f`, out.txt there before (`PREEXISTING`) | 22 | same | kept, `PREEXISTING` (never opened) |
| 404 with `--fail-with-body` | 22 | same | removed |
| 404 without `-f` | 0 | (none) | kept, `nope!` |
| `Content-Length: 10`, 5 bytes, close | 18 | `curl: (18) end of response with 5 bytes missing` | removed |
| same, out.txt there before | 18 | same | removed |
| same, `--remove-on-error --no-remove-on-error` | 18 | same | kept, `hello` |
| failed connect (port 1), out.txt there before | 7 | `curl: (7) Failed to connect to 127.0.0.1:1 after 2012 ms: Could not connect to server` | kept, `PREEXISTING` |
| cut short, no `-o` | 18 | same (18) line | stdout got `hello` |
| cut short, `-v` | 18 | ...`curl: (18) ...`, then `Note: Removed output file: C:\Users\Stewart ` / `Note: Rogers\AppData\Local\Temp\bl494\out.txt` (wrapped at 79) | removed |
| cut short, default meter (no `-s`, no `-v`) | 18 | meter, `curl: (18) ...`, no note | removed |
| `--fail-with-body -s -v` | 22 | no `curl: (22)` line, `Note: Removed output file: out.txt` | removed |
| cut short, `-sSv -w '%{stderr}WE'` | 18 | `curl: (18) ...`, `Note: Removed ...`, then `WE` | removed; `%{filename_effective}` still names it |
| cut short, `-# -v` | 18 | `curl: (18) ...`, empty line (bar), `Note: Removed ...` | removed |
| cut short, `-o NUL` | 18 | meter, `curl: (18) ...`, `Warning: Failed removing: NUL` | - |
| cut short, file held open by another process, `-v` | 18 | `curl: (18) ...`, `Warning: Failed removing: C:\Users\Stewart ` / `Warning: Rogers\...\locked.txt` | kept |

Parse-time (curl 8.21.0, exit 2): `--remove-on-error -C -` (or `-C 5`, `--continue-at 5`) gives
`curl: --continue-at is mutually exclusive with --remove-on-error`, `curl: option -C: is badly used here`
(the second option as spelled), try-help. `-C 5 --remove-on-error` and `-C 0`/`-C -` first name
`--remove-on-error`. `-s` hides the first line, `-s -S` does not. `-C 5 --no-remove-on-error`,
`--no-remove-on-error -C 5` and `--remove-on-error --no-remove-on-error -C 5` are accepted.
`-r 0-4 --remove-on-error -C 5` names `--range` (checked first). The task's "exit 18 with `-C`"
case is therefore a refusal, not a transfer.

Decisions (defaults, Claude): the rule is "a failed transfer removes the file it opened" -
curl only deletes what it opened, which is what the pre-existing-file rows show. The runner keeps
`transferOpenedOutputFile` from `DeferredOutputFileStream.IsOpen` and deletes through the new
`IOutputPaths.TryDeleteFile` after the failure lines and the progress-bar newline, before `-w`.
The note shows under `-v`/`--trace` even with `-s` (as the BL-493 note does); the warning is
hidden by `-s`. `TryDeleteFile` returns false for anything that is not a file (so `NUL` gives
`Failed removing`, as measured); curl off Windows is believed to print a different warning for a
non-regular file such as `/dev/null`, which could not be measured here - filed as BL-752.
`WriteRetryWarningAsync` was renamed `WriteWarningUnlessSilentAsync`, since it now writes this
warning too. No ADR: no choice here departs from measured curl.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --remove-on-error parses and deletes the -o/-O file a failed transfer opened, with curl 8.21.0's note, warning and -C refusal
