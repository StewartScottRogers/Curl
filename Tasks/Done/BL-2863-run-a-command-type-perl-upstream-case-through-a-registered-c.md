---
id: BL-2863
title: Run a <command type="perl"> upstream case through a registered C# script interpretation, with %CURL bound to the in-process curl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Conformance.UnitTests/UpstreamCaseScreeningTests.cs, Curl.Conformance.UnitTests/UpstreamCaseRunnerTests.cs]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2863 — Run a <command type="perl"> upstream case through a registered C# script interpretation, with %CURL bound to the in-process curl

## Goal

`UpstreamCaseScreening` and `UpstreamCaseRunner` run a `<command type="perl">` upstream case whose script (`%SRCDIR/test<n>.pl`) has a registered C# interpretation, with `%CURL` bound to the in-process curl, and still skip one with no interpretation as `the harness does not run a perl command`.

## Context

Split from BL-2850 (cases 971, 1478, 1486, 1707, 1708, 1710 of curl 8.22.0). Today such cases skip with `the harness does not run a perl command` (971, 1478, 1486) or `the harness has no value for %CURL` (1707, 1708, 1710). Model the registry on how `UpstreamTest610Script`, `UpstreamTest613Script` and `UpstreamPerlOneLiner` are dispatched in `UpstreamCaseRunner.cs` (the `["PERL"] = UpstreamPerlOneLiner.Program` map). The interpretations themselves are BL-2865 and BL-2866; this task adds the dispatch seam and pins it with a test-only interpretation. `%CURL` stands for the program under test: bind it to a token the interpretation receives, so it can run `curl -h <option>` in process. Never run Perl or Python.

## Acceptance criteria

- [x] `UpstreamCaseScreening` no longer skips a `<command type="perl">` case whose script has a registered C# interpretation, and still skips one with none with the reason `the harness does not run a perl command`; tests in `UpstreamCaseScreeningTests` pin both.
- [x] A `<command type="perl">` case whose line holds `%CURL` gets the in-process curl, not the skip `the harness has no value for %CURL`; a test in `UpstreamCaseRunnerTests` pins it.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Curl.Conformance.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Plan (direct, no subagents to stay inside the cost cap): `UpstreamCaseRunner` takes `perlCommandScripts`, keyed by script file name (`UpstreamCaseScreening.PerlScriptName`); a registered interpretation runs in place of curl with the words after the script and the in-process curl delegate. `%CURL` is always the token `UpstreamCaseRunner.CurlProgram` (`curl`). Decision recorded in ADR-0714.
- Screening: `FindSkipReason(..., interpretsPerlScript)`; `StandIns`, `OrNone` and `UnrunCommandType` extracted to keep `FindSkipReason` and `UnsupportedCommand` at complexity 10 or under.
- Measured: Curl.Conformance.UnitLibrary 100% line, 100% branch, 0 failing members, worst CRAP 10. Build -warnaserror clean, fast tests green.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 42fc4f8a 704c30a1 309149d6.
- 2026-10-19: Doing -> Done. A <command type="perl"> case runs a registered C# script interpretation with %CURL bound to the in-process curl; unregistered scripts still skip
