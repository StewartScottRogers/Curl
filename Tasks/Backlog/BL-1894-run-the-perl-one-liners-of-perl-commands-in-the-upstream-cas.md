---
id: BL-1894
title: Run the Perl one-liners of %PERL commands in the upstream case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1930, BL-1931, BL-1932, BL-1933]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1894 — Run the Perl one-liners of %PERL commands in the upstream case runner

## Goal

The runner runs the Perl one-liners and perl-invoking checks that upstream cases use (%PERL), so the 23 cases that call perl are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 23 cases use %PERL, plus any precheck/postcheck left by BL-1893. No Perl is installed in the harness and none may be required, so interpret the Perl the cases actually use: collect every distinct %PERL invocation (perl -e one-liners, scripts under tests/ called with arguments) from tests/data, and implement exactly those forms as a small C# interpreter or per-script emulation. UpstreamPerlSubstitution.cs already interprets s/// strip lines; follow its style. Forms you cannot reproduce faithfully stay skipped with the form named. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [ ] Unit tests cover each Perl form implemented, and at least 10 named upstream cases using %PERL run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [ ] UpstreamCaseScreening no longer returns a skip reason of the form "the harness does not run the Perl" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [ ] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [ ] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [ ] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- 2026-10-09 (lane 2): Split rather than started. The 31 vendored cases that use %PERL need three separate emulations - the perl -e one-liners (stat mtime checks, print-if prechecks, grep counts, file generators), tests/libtest/test610.pl (mkdir, rmdir, move, gone, rm) and tests/libtest/test613.pl (prepare, postprocess) - each with its own coverage-held tests; together they do not fit one lane run. Filed as BL-1930, BL-1931 and BL-1932. What is left here once they are Done: run at least 10 named %PERL cases through UpstreamCaseRunner and the interactive Measure-UpstreamCases.cs check. test1013.pl and test1022.pl compare against ../curl-config and are expected to stay skipped with that reason.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Backlog. Split into BL-1930, BL-1931 and BL-1932 (one-liners, test610.pl, test613.pl); resumes once they are Done
