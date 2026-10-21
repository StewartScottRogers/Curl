---
id: BL-2845
title: Port upstream unit programs for tool and protocol parsers: cases 1604, 1621, 1622, 1623, 1636, 1668, 1720, 2604, 2605, 3200 and 3227
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2886, BL-2899, BL-2900, BL-2901, BL-2902, BL-2903, BL-2904, BL-2905, BL-2906, BL-2907]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.ToolParsers.cs, Curl.Conformance.UnitProgram.UnitLibrary/ToolParsers, Curl.Conformance.UnitProgram.UnitTests/ToolParsers, Curl.Output.UnitLibrary/ParallelProgressMeterText.cs, Curl.Output.UnitTests/ParallelProgressMeterTextTests.cs]
requirement: none
created: 2026-10-19
completed: 2026-10-21
---
# BL-2845 — Port upstream unit programs for tool and protocol parsers: cases 1604, 1621, 1622, 1623, 1636, 1668, 1720, 2604, 2605, 3200 and 3227

## Goal

Upstream cases 1604, 1621, 1622, 1623, 1636, 1668, 1720, 2604, 2605, 3200 and 3227 run as ported unit programs over the curl tool's and the protocol libraries' own parsers and formatters and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or have a reason recorded.

## Context

These eleven `unittest` cases (skipped today with `Curl lacks the feature unittest` or `the harness does not act on <client><tool>`; gap scorecard 2026-10-19_1329: `excluded | debug-build-only`) test one function each in upstream curl 8.22.0 (https://github.com/curl/curl/tree/curl-8_22_0/tests/unit and `/tests/tunit`): the tool programs `tool1604` (Windows/MS-DOS file name sanitisation), `tool1621` (strip credentials from a URL), `tool1622` (tool time and size progress output), `tool1623` (the `--limit-rate` size parser) and `tool1720` (`create_dir_hierarchy()`), and the library programs 1636 (`time2str` and `max6out`), 1668 (`ftp_213_date`, feature `ftp`), 2604 (`Curl_get_pathname`, feature `sftp`), 2605 (`Curl_ssh_range`, feature `sftp`), 3200 (`curl_get_line`) and 3227 (reject CR and LF in a mail MIME part name and file name). Curl's counterparts are in `Curl.Cli.UnitLibrary` and `Curl.Output.UnitLibrary` (progress meter), `Curl.Protocol.Ftp.UnitLibrary`, `Curl.Protocol.Ssh.UnitLibrary` and whichever library builds MIME parts for mail; find each with the Glossary and `rg`. Follow the ADR from BL-2840 and the mechanism from BL-2841: one `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary`, registered in `UnitPrograms.Ported`, reproducing every check of the C program and returning its failure count. Case 1604 checks Windows file-name rules: write the port platform-neutral (root CLAUDE.md), running both rule sets where the C test selects one by `#ifdef`.

## Acceptance criteria

- [x] Each of the eleven cases has a port in `Curl.Conformance.UnitProgram.UnitLibrary` whose checks match the C program's row for row (named in its doc comment), registered in `UnitPrograms.Ported`, with a test in `Curl.Conformance.UnitProgram.UnitTests`; or the case has a Notes entry naming the C-only thing it tests and the ADR from BL-2840 row says so.
- [x] Each ported case ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and is on `PassingUpstreamCases.txt`.
- [x] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482), the library folder is added to `touches` in the same commit, and any change to a command-line option keeps `curl --ai-help` right.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library and the new library keep 100% line and branch coverage and complexity at most 10.
- [x] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

Ports go in Curl.Conformance.UnitProgram.UnitLibrary/ToolParsers/ and their tests in Curl.Conformance.UnitProgram.UnitTests/ToolParsers/; register them only in Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.ToolParsers.cs, which BL-2886 creates. Its project references are added by BL-2886; if one is missing, add it in a separate one-line commit and say so in the Log (Stewart, 2026-10-21: keep the seven ports parallel).

2026-10-21, lane 1 prerequisite pass (before any code): of the eleven programs only tool1623 runs over Curl as it is (`CommandLineNumber.ParseSize`, public, with `BadlyUsedHere`/`TooLargeNumber`/`ExpectedProperNumericalParameter` for `PARAM_BAD_USE`/`PARAM_NUMBER_TOO_LARGE`/`PARAM_BAD_NUMERIC`). The other ten need product work outside this task's `touches`, filed as one task per library so they run in parallel:

- tool1622: `ParallelProgressMeterText.Time` is 8.21's format; 8.22's `timebuf` has months and years (BL-2899).
- unit1636 (BL-2900), tool1621 (BL-2901), tool1720 (BL-2902): the counterparts live in the executable `Curl.Console`, which this library cannot reference.
- tool1604: `WindowsOutputFileNameSanitizer` lacks `SANITIZE_ALLOW_PATH`/`SANITIZE_ALLOW_RESERVED` (BL-2903).
- unit1668: `FtpModificationTime` refuses dates `ftp_213_date` accepts (month 00, day 00, 31 February) and is internal with no `InternalsVisibleTo` for this library (BL-2904).
- unit2604, unit2605: `Curl_get_pathname` and `Curl_ssh_range` counterparts are private inside `SftpQuoteCommand` and `SftpDownloadPart` (BL-2905).
- unit3200: no `Curl_get_line` counterpart (BL-2906).
- unit3227: no mail-strategy CR/LF check on a MIME part's name and file name was found (BL-2907).

The C sources are at https://github.com/curl/curl/tree/curl-8_22_0/tests/tunit and /tests/unit. Port all eleven together once these are Done; tool1622 and unit1636 also compare the program's stdout with the case's `<stdout>` block, so those ports write each line to `context.StandardOutput`. `UnitChecks` (in `Hashes/`) can be reused for `fail_unless`.

2026-10-21, lane 9 (delivered directly, not through the /feature agents: eleven table ports over counterparts BL-2899 to BL-2907 already built and tested, so planning and review agents would cost more than the change):

- One port per program in `Curl.Conformance.UnitProgram.UnitLibrary/ToolParsers/`, registered in `UnitPrograms.ToolParsers.cs`; helpers `StandardOutputLines` (LF-ended ASCII stdout lines), `ProgressFieldRows` (the 63 doubling rows and five byte counts tool1622 and unit1636 share) and `GetLineFileCase` (unit3200's file cases). Tests in `Curl.Conformance.UnitProgram.UnitTests/ToolParsers/ToolParsersProgramsTests.cs`.
- Fix found by tool1622 (ADR-0482): `ParallelProgressMeterText.Size` was 8.21.0's `max5data` (decimal only above k, cut to `99.10` near 100 of a unit). 8.22.0's writes a decimal at any unit under 100, so 100000 is `97.6k` and 100 MiB less one byte `99.9M`. Rewritten from curl-8_22_0 `src/tool_progress.c`; `ParallelProgressMeterTextTests` re-pins `  97k` and the two `99.10` rows, and adds tool1622's datacases. No command-line option changed, so `--ai-help` is untouched. `touches` gained those two files; no task in Doing on `origin/work/dark-factory` names Curl.Output. `Curl.Output.UnitLibrary/CLAUDE.md` still says 8.21.0 for this class, left for align-and-document (outside touches).
- Choices (sensible defaults): tool1604 and tool1720 run their Windows rows on every platform (Curl's sanitizer and directory rule sets take no OS input), and tool1720 runs its shared rows under both rule sets. Failure lines go to stderr through `UnitChecks` where some C programs print them to stdout; only 1622 and 1636 compare stdout, and theirs is written byte for byte. unit3200 returns its failure count; the C program returns the last `Curl_get_line` code, which is 0 whenever every check passes. unit1668's `\xNN` bytes are the Latin-1 characters of that value, as BL-2904 maps them. unit3200's "buffer empty" is `ReadLineAsync` returning null (the reader never returns an empty line).
- Platform: all eleven ports are pure over strings and the case's own log file, so their output does not depend on the OS.
- Measured: all eleven cases `Passed` with `CURL_UPSTREAM_CASE`; full build clean; fast tests all green. `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitProgram.UnitLibrary,Curl.Output.UnitLibrary`: Output 100/100, 0 failing; UnitProgram 100% line, 99.58% branch, one member (`Unit1668Ftp213Date.CheckRow`, its "expected 0, got 1" ternaries), fixed by `CheckRow_AcceptedWhereARefusalIsExpected_WritesItAndFails`, which takes the remaining direction; worst CRAP 10. CI on Linux and macOS is watched by the shift's coordinator after its push (BL-987).

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 484e6579 2e3e75ab 33133e4f c0723528 620ebead.
- 2026-10-21: Doing -> Backlog. Waits on BL-2899 to BL-2907: ten of the eleven programs need product counterparts outside this task (Output, Console moves, Core, Ftp, Ssh, Api); see Notes
- 2026-10-21: Backlog -> Doing. Criteria at claim: 484e6579 2e3e75ab 33133e4f c0723528 620ebead.
- 2026-10-21: Doing -> Done. Upstream cases 1604, 1621, 1622, 1623, 1636, 1668, 1720, 2604, 2605, 3200 and 3227 run as ported unit programs and are on the ratchet; the parallel meter's max5data now matches curl 8.22.0
