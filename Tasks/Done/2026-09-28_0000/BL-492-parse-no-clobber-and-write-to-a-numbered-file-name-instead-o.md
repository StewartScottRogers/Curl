---
id: BL-492
title: Parse --no-clobber and write to a numbered file name instead of overwriting
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-492 — Parse --no-clobber and write to a numbered file name instead of overwriting

## Goal

With `--no-clobber`, an `-o`/`-O` target that already exists is left alone and the body goes to the first free `<name>.1` … `<name>.100`, as curl 8.21.0 does; `--clobber` restores overwriting.

## Context

- Conformance audit 2026-09-28, row 11 (Blocker, S).
- Alias-table row `clobber`, `--no-` documented (`Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`); no row in `CommandLineOptionTable.cs`.
- Output files are opened in `Curl.Console` (`OutputFileTarget.cs`, `DeferredOutputFileStream.cs`, `IOutputPaths.cs`, `PhysicalOutputPaths.cs`). Use the existing `IOutputPaths` seam so tests need no disk.
- The manual text (`CurlManual.txt`, `--no-clobber`) states the numbering and the 100 limit; what curl prints and exits when all 100 exist must be measured.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` (loopback 200, `-o out.txt --no-clobber`): target absent, target present, `out.txt` and `out.txt.1` present, and all of `out.txt`, `.1` … `.100` present; stdout, stderr, exit code and the files left behind copied into Notes.
- [x] `--no-clobber` and `--clobber` parse (last wins); `Curl.Cli.UnitTests` covers both.
- [x] `Curl.Console.UnitTests` tests through `IOutputPaths` pin each measured case: the file chosen, the untouched original, and the stderr and exit code when no name is free.
- [x] `%{filename_effective}` reports the numbered name actually written.
- [x] New tests are platform-neutral (no drive-letter paths).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli.UnitLibrary` and `Curl.Console`.

## Notes

Measured 2026-09-28 with the local curl 8.21.0 (mingw, Schannel) through `Record-CurlExchange.ps1`,
loopback 200 with a 5-byte `hello` body, `-sS -o <dir>\out.txt --no-clobber -w %{filename_effective}`:

| Case | Exit | stdout (`%{filename_effective}`) | stderr | Files left |
| --- | --- | --- | --- | --- |
| target absent | 0 | `out.txt` | empty | `out.txt`=hello |
| target present | 0 | `out.txt.1` | empty | `out.txt`=orig, `out.txt.1`=hello |
| `out.txt`, `.1` present | 0 | `out.txt.2` | empty | `.1` untouched, `.2`=hello |
| `out.txt`, `.1`..`.98` present | 0 | `out.txt.99` | empty | `.99`=hello |
| `out.txt`, `.1`..`.99` present (`.100` absent) | 23 | `out.txt.99` | `curl: (23) client returned ERROR on write of 5 bytes` | all untouched, no `.100` |
| `out.txt`, `.1`..`.100` present | 23 | `out.txt.99` | same | all untouched (101 files) |
| same, `--no-progress-meter` instead of `-sS` | 23 | `out.txt.99` | `Warning: Failed to open the file <dir>\out.txt: File exists` (wrapped) then the (23) line | all untouched |
| `--no-clobber --clobber`, target present | 0 | - | empty | `out.txt`=hello (overwritten) |
| empty body (Content-Length 0), target present | 0 | `out.txt.1` | empty | `out.txt.1` empty |
| target is a directory | 23 | `out.txt` | the (23) line | no numbering |
| `-OJ` with `cd.txt` taken, `--clobber` | 0 | `.../cd.txt` | empty | `cd.txt` overwritten |
| `-OJ` with `cd.txt` taken, `--no-clobber` | 0 | `.../cd.txt.1` | empty | `cd.txt.1`=hello |
| `-OJ` with `cd.txt` taken, neither | 23 | `.../cd.txt` | `(23) ... write of 52 bytes` | kept |

Findings: curl tries `.1` to `.99` only - the manual's "100" is off by one, and the source's
loop is `next_num < 100`. When none is free, `%{filename_effective}` names the last name
tried (`.99`) and the warning names the file asked for. `--clobber` also overwrites a `-J`
name, which curl otherwise refuses.

Decisions (matching the measured Windows curl, no ADR needed beyond this):
- `DeferredOutputFileStream` takes `bool? clobber` (null = neither option). Under
  `--no-clobber` a whole-transfer open is `FileWriteMode.CreateNew` and an `AlreadyExists`
  answer moves on to the numbered names; any other failure (a directory, a missing
  parent) stops without numbering, as the measured directory case does on Windows. On
  Linux, `open(O_CREAT|O_EXCL)` on a directory answers `EEXIST`, so curl there would number
  past a directory too; whether `PhysicalFileSystem` reports that as `AlreadyExists` off
  Windows is not pinned here.
- A `-C` resume still appends to the named file under `--no-clobber`, as curl's resume
  open (`"ab"`) does not go through its clobber logic.
- The tests use the `InMemoryFileSystem` fake, which is both the runner's `IFileSystem`
  and its `IOutputPaths`; the open goes through `IFileSystem.OpenForWriteAsync`, so no
  change to `IOutputPaths` was needed.

Tests: `Curl.Cli.UnitTests/CommandLineClobberTests.cs` (5), 
`Curl.Console.UnitTests/CurlCommandRunnerNoClobberTests.cs` (13). Build clean with
`-warnaserror`, fast tests green, `Measure-CodeQuality.ps1`: `Curl.Cli.UnitLibrary` and
`Curl.Console` 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --no-clobber keeps an existing -o/-O/-J file and writes the first free <name>.1..99 (exit 23 when none), --clobber overwrites, last wins
