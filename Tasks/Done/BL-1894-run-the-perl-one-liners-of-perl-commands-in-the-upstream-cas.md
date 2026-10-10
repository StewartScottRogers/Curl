---
id: BL-1894
title: Run the Perl one-liners of %PERL commands in the upstream case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1930, BL-1931, BL-1932, BL-1933, BL-1944]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-10
---
# BL-1894 — Run the Perl one-liners of %PERL commands in the upstream case runner

## Goal

The runner runs the Perl one-liners and perl-invoking checks that upstream cases use (%PERL), so the 23 cases that call perl are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 23 cases use %PERL, plus any precheck/postcheck left by BL-1893. No Perl is installed in the harness and none may be required, so interpret the Perl the cases actually use: collect every distinct %PERL invocation (perl -e one-liners, scripts under tests/ called with arguments) from tests/data, and implement exactly those forms as a small C# interpreter or per-script emulation. UpstreamPerlSubstitution.cs already interprets s/// strip lines; follow its style. Forms you cannot reproduce faithfully stay skipped with the form named. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] Unit tests cover each Perl form implemented, and at least 10 named upstream cases using %PERL run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [x] UpstreamCaseScreening no longer returns a skip reason of the form "the harness does not run the Perl" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [x] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [x] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- 2026-10-09 (lane 2): Split rather than started. The 31 vendored cases that use %PERL need three separate emulations - the perl -e one-liners (stat mtime checks, print-if prechecks, grep counts, file generators), tests/libtest/test610.pl (mkdir, rmdir, move, gone, rm) and tests/libtest/test613.pl (prepare, postprocess) - each with its own coverage-held tests; together they do not fit one lane run. Filed as BL-1930, BL-1931 and BL-1932. What is left here once they are Done: run at least 10 named %PERL cases through UpstreamCaseRunner and the interactive Measure-UpstreamCases.cs check. test1013.pl and test1022.pl compare against ../curl-config and are expected to stay skipped with that reason.

- 2026-10-09 (lane 1): Measured all 31 vendored %PERL cases through the conformance run (881 pass, 1132 skip in the whole run). Measured now: 762, 1026, 1027, 1082, 1291, 1443, 1683 pass, test8 fails on a real Curl difference (Cookie header at byte 201), test2072's precheck runs and skips on Windows with "Test requires a Unix system" (measured off Windows) - 8 on Windows, 9 off it, one short of 10. Of the rest: 608-615, 624, 625, 627, 638, 639, 1446, 1583 need the SFTP server (%SSHPORT, %USER, %SFTP_PWD); 1013/1014/1022/1023 compare with ../curl-config; 1444 needs an FTP data connection; 1083 needs %CLIENT6IP; 307 needs HTTPS and %CURL. The one reachable case, test1445 (file://), needs %PWD and %SRCDIR values, and its %PWD/%LOGDIR cannot name a real path while %LOGDIR is absolute (31 cases hit that), plus curl's output named curl%TESTNUMBER.out: a runner change beyond Perl, filed as BL-1944.
- Done here, left uncommitted for the next claim (rule 6): UpstreamPerlCheckLine routes a %PERL check line to UpstreamPerlOneLiner or, when its program is test610.pl / test613.pl in whatever folder, to UpstreamTest610Script / UpstreamTest613Script; screening and the runner use it; ScriptName is public on both emulations. Tests: UpstreamPerlCheckLineTests (every branch) and a screening test that expanded test610.pl/test613.pl check lines are no skip reason. Build clean with -warnaserror; Curl.Conformance.UnitTests green (2005 passed). Still to do once BL-1944 is Done: a conformance test pinning the 10 measured %PERL cases (as SetenvCase_RunThroughCurl_IsMeasuredNotSkipped does), CLAUDE.md (the "Nothing calls the two script emulations yet" sentence), then the fast suite.
- 2026-10-09 (BL-1944): `%PWD/%LOGDIR` and `%SRCDIR/libtest/test61[03].pl` now compose (ADR-0458): a check line reaches the runner as `perl ./libtest/test613.pl prepare <abs logdir>/test1445.dir`, and curl writes `%LOGDIR/curl%TESTNUMBER.out`. With the stashed UpstreamPerlCheckLine routing applied, test1445 should be measured; measure it here and add it to PassingUpstreamCases.txt if it passes (moved from BL-1944).

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Backlog. Split into BL-1930, BL-1931 and BL-1932 (one-liners, test610.pl, test613.pl); resumes once they are Done
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Backlog. Waits on BL-1944: test1445, the tenth measurable %PERL case, needs %PWD/%SRCDIR values that compose with the absolute %LOGDIR
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. Ten %PERL cases measured through the runner; test1445 passes and is listed
