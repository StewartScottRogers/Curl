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
completed:
---
# BL-2863 — Run a <command type="perl"> upstream case through a registered C# script interpretation, with %CURL bound to the in-process curl

## Goal

`UpstreamCaseScreening` and `UpstreamCaseRunner` run a `<command type="perl">` upstream case whose script (`%SRCDIR/test<n>.pl`) has a registered C# interpretation, with `%CURL` bound to the in-process curl, and still skip one with no interpretation as `the harness does not run a perl command`.

## Context

Split from BL-2850 (cases 971, 1478, 1486, 1707, 1708, 1710 of curl 8.22.0). Today such cases skip with `the harness does not run a perl command` (971, 1478, 1486) or `the harness has no value for %CURL` (1707, 1708, 1710). Model the registry on how `UpstreamTest610Script`, `UpstreamTest613Script` and `UpstreamPerlOneLiner` are dispatched in `UpstreamCaseRunner.cs` (the `["PERL"] = UpstreamPerlOneLiner.Program` map). The interpretations themselves are BL-2865 and BL-2866; this task adds the dispatch seam and pins it with a test-only interpretation. `%CURL` stands for the program under test: bind it to a token the interpretation receives, so it can run `curl -h <option>` in process. Never run Perl or Python.

## Acceptance criteria

- [ ] `UpstreamCaseScreening` no longer skips a `<command type="perl">` case whose script has a registered C# interpretation, and still skips one with none with the reason `the harness does not run a perl command`; tests in `UpstreamCaseScreeningTests` pin both.
- [ ] A `<command type="perl">` case whose line holds `%CURL` gets the in-process curl, not the skip `the harness has no value for %CURL`; a test in `UpstreamCaseRunnerTests` pins it.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Curl.Conformance.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-19: Created.
