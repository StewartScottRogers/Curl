---
id: BL-2845
title: Port upstream unit programs for tool and protocol parsers: cases 1604, 1621, 1622, 1623, 1636, 1668, 1720, 2604, 2605, 3200 and 3227
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2886, BL-2899, BL-2900, BL-2901, BL-2902, BL-2903, BL-2904, BL-2905, BL-2906, BL-2907]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.ToolParsers.cs, Curl.Conformance.UnitProgram.UnitLibrary/ToolParsers, Curl.Conformance.UnitProgram.UnitTests/ToolParsers]
requirement: none
created: 2026-10-19
completed:
---
# BL-2845 — Port upstream unit programs for tool and protocol parsers: cases 1604, 1621, 1622, 1623, 1636, 1668, 1720, 2604, 2605, 3200 and 3227

## Goal

Upstream cases 1604, 1621, 1622, 1623, 1636, 1668, 1720, 2604, 2605, 3200 and 3227 run as ported unit programs over the curl tool's and the protocol libraries' own parsers and formatters and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or have a reason recorded.

## Context

These eleven `unittest` cases (skipped today with `Curl lacks the feature unittest` or `the harness does not act on <client><tool>`; gap scorecard 2026-10-19_1329: `excluded | debug-build-only`) test one function each in upstream curl 8.22.0 (https://github.com/curl/curl/tree/curl-8_22_0/tests/unit and `/tests/tunit`): the tool programs `tool1604` (Windows/MS-DOS file name sanitisation), `tool1621` (strip credentials from a URL), `tool1622` (tool time and size progress output), `tool1623` (the `--limit-rate` size parser) and `tool1720` (`create_dir_hierarchy()`), and the library programs 1636 (`time2str` and `max6out`), 1668 (`ftp_213_date`, feature `ftp`), 2604 (`Curl_get_pathname`, feature `sftp`), 2605 (`Curl_ssh_range`, feature `sftp`), 3200 (`curl_get_line`) and 3227 (reject CR and LF in a mail MIME part name and file name). Curl's counterparts are in `Curl.Cli.UnitLibrary` and `Curl.Output.UnitLibrary` (progress meter), `Curl.Protocol.Ftp.UnitLibrary`, `Curl.Protocol.Ssh.UnitLibrary` and whichever library builds MIME parts for mail; find each with the Glossary and `rg`. Follow the ADR from BL-2840 and the mechanism from BL-2841: one `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary`, registered in `UnitPrograms.Ported`, reproducing every check of the C program and returning its failure count. Case 1604 checks Windows file-name rules: write the port platform-neutral (root CLAUDE.md), running both rule sets where the C test selects one by `#ifdef`.

## Acceptance criteria

- [ ] Each of the eleven cases has a port in `Curl.Conformance.UnitProgram.UnitLibrary` whose checks match the C program's row for row (named in its doc comment), registered in `UnitPrograms.Ported`, with a test in `Curl.Conformance.UnitProgram.UnitTests`; or the case has a Notes entry naming the C-only thing it tests and the ADR from BL-2840 row says so.
- [ ] Each ported case ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and is on `PassingUpstreamCases.txt`.
- [ ] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482), the library folder is added to `touches` in the same commit, and any change to a command-line option keeps `curl --ai-help` right.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library and the new library keep 100% line and branch coverage and complexity at most 10.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

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

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 484e6579 2e3e75ab 33133e4f c0723528 620ebead.
- 2026-10-21: Doing -> Backlog. Waits on BL-2899 to BL-2907: ten of the eleven programs need product counterparts outside this task (Output, Console moves, Core, Ftp, Ssh, Api); see Notes
- 2026-10-21: Backlog -> Doing. Criteria at claim: 484e6579 2e3e75ab 33133e4f c0723528 620ebead.
