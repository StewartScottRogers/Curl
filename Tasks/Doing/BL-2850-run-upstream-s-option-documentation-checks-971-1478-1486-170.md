---
id: BL-2850
title: Run upstream's option-documentation checks 971, 1478, 1486, 1707, 1708 and 1710 against Curl's option, help and write-out tables
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Conformance.UnitTests/UpstreamCaseScreeningTests.cs, Curl.Conformance.UnitTests/UpstreamTestData/README.md]
requirement: none
created: 2026-10-19
completed:
---
# BL-2850 — Run upstream's option-documentation checks 971, 1478, 1486, 1707, 1708 and 1710 against Curl's option, help and write-out tables

## Goal

The harness runs a `<command type="perl">` case by interpreting the named upstream check script in C# (never by running Perl or Python), and upstream cases 971, 1478, 1486, 1707, 1708 and 1710 check Curl's option, help and write-out tables against upstream's documentation, each on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or with a recorded reason.

## Context

Gap scorecard 2026-10-19_1329 has these six `excluded` (971, 1478 and 1486 `upstream-source-check`; 1707, 1708 and 1710 `reference-lacks:manual`); measured 2026-10-19 on Windows they are skipped with `the harness does not run a perl command` (971, 1478, 1486) or `the harness has no value for %CURL` (1707, 1708, 1710). Upstream curl 8.22.0 (https://github.com/curl/curl/tree/curl-8_22_0/tests) checks its own documentation against itself:

- 971 "options-in-versions and docs/cmdline-opts are in sync": `%SRCDIR/test971.pl` over `docs/options-in-versions`, `docs/cmdline-opts` and `docs/VERSIONS.md`; Curl's counterpart is its option table (`Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`, `CurlOptionAliasTable`), which the gap office's options area already keeps equal to upstream.
- 1478 "src/tool_listhelp.c is in sync with docs/cmdline-opts": the expected standard output is the C source of `tool_listhelp.c`; Curl's counterpart is `CurlHelpTable`, which has no C source - judge whether the case is portable as "`--help all` lists each option's `Help:` line from the docs" and record the reason if not.
- 1486 "write-out.md and tool_writeout.c are in sync": expected standard output `OK`; counterpart is Curl's write-out variable table.
- 1707, 1708 and 1710 "Verify curl -h --insecure / -h -F / -h --no-clobber": `%SRCDIR/test1707.pl %CURL <option> ../docs/cmdline-opts/curl.txt` runs `curl -h <option>` and compares with the option's text in the generated `curl.txt`; `%CURL` is the program under test.

What is needed: (1) vendor the documentation files the scripts read (`docs/options-in-versions`, `docs/VERSIONS.md`, `docs/cmdline-opts/*.md`, and whatever else the scripts open) next to `UpstreamTestData/` by extending `Curl.Conformance.UnitTests/UpstreamTestData/Update-UpstreamTestData.ps1` and its `README.md` (licence `COPYING` already there; tests never download anything, ADR-0013 decision 3); (2) teach `UpstreamCaseScreening` and `UpstreamCaseRunner` to run a `<command type="perl">` whose script the harness has a C# interpretation for (the way `UpstreamTest610Script` and `UpstreamTest613Script` interpret two libtest scripts, `UpstreamPerlOneLiner` the one-liners), with `%CURL` bound to the in-process curl; (3) write the interpretations of `test971.pl`, `test1486.pl` and `test1707.pl` (read each at tag `curl-8_22_0`), over Curl's tables. Curl's help and option names must match real curl 8.22.0's; where the check finds Curl's table differing from the docs, fix Curl (with a focused unit test in `Curl.Cli.UnitTests`, ADR-0482) and keep `curl --ai-help` right (root CLAUDE.md).

If six cases prove too many for one run, finish 971, 1486, 1707, 1708 and 1710 first and file 1478 as a new task.

## Acceptance criteria

- [ ] `UpstreamCaseScreening` no longer skips a `<command type="perl">` case whose script has a C# interpretation, and still skips one that has none with the reason `the harness does not run a perl command`; tests pin both.
- [ ] The vendored documentation files are listed in `UpstreamTestData/README.md`, copied beside the tests by the csproj, and refreshed by `Update-UpstreamTestData.ps1`; `UpstreamTestDataTests` pins their presence.
- [ ] Each of the six cases ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and is on `PassingUpstreamCases.txt`, or has in Notes the C-only thing it tests (1478 only) and is named in a new ADR "Decided by Claude under Stewart's delegation".
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: ec51dafc a075cc9b cd7380ad dd1cc079 620ebead.
