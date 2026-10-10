---
id: BL-1893
title: Act on perl-free precheck and postcheck commands in the upstream case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1929]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1893 — Act on perl-free precheck and postcheck commands in the upstream case runner

## Goal

The runner runs <client><precheck> and <verify><postcheck> parts that are not Perl programs, so the cases skipped for them are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. Skipped today: 7 cases for <client><precheck> and 8 for <verify><postcheck>. BL-1894 handles %PERL commands and any precheck/postcheck whose body is a perl invocation. First list every precheck and postcheck body in tests/data and group them (a file-existence test, a command, a perl call, a server-state check); implement the groups that need no Perl interpreter and name the ones left to BL-1894 in the Notes. Semantics per runtests.pl: a failing precheck skips the case with the check's reason; a failing postcheck fails it. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [ ] Unit tests cover each group implemented (success, failing precheck skipping the case, failing postcheck failing it), and at least 5 named upstream cases using precheck or postcheck run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [ ] UpstreamCaseScreening no longer returns a skip reason of the form "the harness does not act on <client><precheck>" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [ ] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [ ] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [ ] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- 2026-10-09 (lane 2) inventory of every `<precheck>` / `<postcheck>` in the vendored 8.21.0 tests/data (38 cases), grouped:
  - **Perl call** (`%PERL -e ...`, `%PERL %SRCDIR/libtest/test610.pl|test613.pl ...`): test8, 1013, 1014, 1022, 1023, 1026, 1027, 1082, 1083, 1291, 1443-1446, 1583, 1683, 2072, 307, 608, 610-615, 624, 625, 627, 638, 639, 762. Left to BL-1894.
  - **Libtest check** (`%LIBTESTS lib%TESTNUMBER check`): test518, 537, 678, 1960. These are `<tool>` cases, skipped for the libtest first; acting on the precheck measures nothing until the harness runs libtests.
  - **runtests.pl self-test shell commands** (`mkdir ...; cp ...; echo ...` precheck, `grep -q ...` postcheck): test1182 only, whose `<command type="perl">` runs runtests.pl itself; the harness cannot run it.
  - **Resolve check** (`%RESOLVE --ipv6 <name>`): test1085, test241 (test241 also needs the `http-ipv6` server). The only perl-free group the in-process harness can act on; filed as BL-1929.
- Decision: the acceptance criterion "at least 5 named upstream cases" cannot be met by perl-free checks: only test1085 (and test241 after an `http-ipv6` stand-in) can become measured. The resolve group is filed as BL-1929 and this task waits on it; once BL-1929 is Done, this task closes with the inventory above (the 5-case criterion moves to BL-1894, whose Perl group holds 31 of the 38 cases).

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Backlog. Waits on BL-1929 (the %RESOLVE precheck, the only perl-free check group the harness can act on); inventory in Notes
- 2026-10-09: Backlog -> Doing.
