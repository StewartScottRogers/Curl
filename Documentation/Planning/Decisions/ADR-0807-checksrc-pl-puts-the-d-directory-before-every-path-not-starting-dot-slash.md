# ADR-0807 — The C# checksrc.pl puts the -D directory before every path not starting `./` or `../`, strips CR LF on every platform, and never warns COPYRIGHTYEAR

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3419 (split from BL-3413; the interpreter is BL-3416, BL-3417 and BL-3418)
- Decided by Claude under Stewart's delegation.

## Context

Upstream case 1185 runs curl 8.22.0's `scripts/checksrc.pl -bmagicbad -balsobad %LOGDIR/code1185.c`
and expects every report line to start `./%LOGDIR/code1185.c:`. The conformance runner interprets the
script in C# (`UpstreamChecksrcScript`, registered under `checksrc.pl` by
`UpstreamPerlCommandScripts.Checksrc.cs`), since lanes run no Perl. Three points of the script do not
carry over to the harness as written:

- checksrc.pl puts `$dir/` (the `-D` directory, `.` by default) in front of a file only when its path
  does not match `^\.?\.?/`. runtests.pl's `%LOGDIR` is relative (`log/1`), so upstream gets
  `./log/1/code1185.c`. The harness's `%LOGDIR` is absolute (`logDirectory.FullName`): on Linux and
  macOS it starts with `/`, so a plain port would leave the `./` off and fail there, while on Windows
  (`Z:/...`) it would add it.
- On Windows the script strips a trailing CR LF; elsewhere only LF, so a CR would reach the line checks.
- `COPYRIGHTYEAR` asks git for the file's last commit year.

## Decision

- Put `-D` directory and `/` in front of every path that does not start with `./` or `../`, absolute
  paths included, so the report reads `./` + `%LOGDIR` on every platform as the expected stdout does.
  An absolute path is opened as it is, not under the directory.
- Strip a trailing CR LF or LF from every line on every platform (the script's Windows rule), so the
  report does not depend on the platform the harness runs on.
- `COPYRIGHTYEAR` never warns: the case's file lies in no git repository, so upstream's check finds no
  commit year to compare and is silent too, and the interpreter runs no git.

## Consequences

- Case 1185 is on `PassingUpstreamCases.txt` and passes with the same bytes on Windows, Linux and macOS.
- A caller passing an absolute path to the interpreter sees `./` before it, which real checksrc.pl does
  not print; no case does that with an expected output other than 1185's.
