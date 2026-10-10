---
id: BL-1943
title: Read --variable %NAME through the run's injected environment reader
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1943 — Read --variable %NAME through the run's injected environment reader

## Goal

`--variable %NAME` (and `%NAME=default`) imports the variable through the environment reader the run was given (`CurlCommandRunner`'s `readEnvironmentVariable`), not the process environment, so upstream cases 428, 448 and 458 run with their `<setenv>` variables.

## Context

`Curl.Cli.UnitLibrary\VariableDefinition.cs:45` calls `Environment.GetEnvironmentVariable(name)` directly. Since BL-1928 the dialing `CurlComposition.CreateRunner` takes a `readEnvironmentVariable`, and since BL-1892 the conformance tests pass each case's `<client><setenv>` through it; every other environment read reaches the injected reader, but `--variable` does not, so tests 428, 448 and 458 (curl 8.21.0, vendored in `Curl.Conformance.UnitTests\UpstreamTestData`) send nothing: `--expand-data` / `--expand-output` see the variables unset and curl stops. Thread the reader from `CurlCommandRunner` into the variable expansion (the console's production composition passes `Environment.GetEnvironmentVariable`, so behaviour outside tests is unchanged). `WrappedMessage.cs` (`COLUMNS`) and `DefaultConfigFileSearch.cs` read the process environment the same way; look at them too and route them through the reader if it is the same seam.

## Acceptance criteria

- [ ] A Curl.Cli.UnitTests test gives a run a reader holding `FUNVALUE=contents` and shows `--variable %FUNVALUE --expand-data {{FUNVALUE}}` sends `contents` without the process environment holding it.
- [ ] No `Environment.GetEnvironmentVariable` call remains in `VariableDefinition.cs`.
- [ ] Curl.Cli.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; any of 428, 448, 458 that now pass are added to `Curl.Conformance.UnitTests\PassingUpstreamCases.txt` only if this task's touches are widened to it (otherwise the next gap run lists them).

## Notes

- Filed by BL-1892 (lane 1, 2026-10-09), which found the cause while measuring the newly un-skipped `<setenv>` cases.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
