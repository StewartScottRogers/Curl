---
id: BL-1943
title: Read --variable %NAME through the run's injected environment reader
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1943 — Read --variable %NAME through the run's injected environment reader

## Goal

`--variable %NAME` (and `%NAME=default`) imports the variable through the environment reader the run was given (`CurlCommandRunner`'s `readEnvironmentVariable`), not the process environment, so upstream cases 428, 448 and 458 run with their `<setenv>` variables.

## Context

`Curl.Cli.UnitLibrary\VariableDefinition.cs:45` calls `Environment.GetEnvironmentVariable(name)` directly. Since BL-1928 the dialing `CurlComposition.CreateRunner` takes a `readEnvironmentVariable`, and since BL-1892 the conformance tests pass each case's `<client><setenv>` through it; every other environment read reaches the injected reader, but `--variable` does not, so tests 428, 448 and 458 (curl 8.21.0, vendored in `Curl.Conformance.UnitTests\UpstreamTestData`) send nothing: `--expand-data` / `--expand-output` see the variables unset and curl stops. Thread the reader from `CurlCommandRunner` into the variable expansion (the console's production composition passes `Environment.GetEnvironmentVariable`, so behaviour outside tests is unchanged). `WrappedMessage.cs` (`COLUMNS`) and `DefaultConfigFileSearch.cs` read the process environment the same way; look at them too and route them through the reader if it is the same seam.

## Acceptance criteria

- [x] A Curl.Cli.UnitTests test gives a run a reader holding `FUNVALUE=contents` and shows `--variable %FUNVALUE --expand-data {{FUNVALUE}}` sends `contents` without the process environment holding it.
- [x] No `Environment.GetEnvironmentVariable` call remains in `VariableDefinition.cs`.
- [x] Curl.Cli.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; any of 428, 448, 458 that now pass are added to `Curl.Conformance.UnitTests\PassingUpstreamCases.txt` only if this task's touches are widened to it (otherwise the next gap run lists them).

## Notes

- Filed by BL-1892 (lane 1, 2026-10-09), which found the cause while measuring the newly un-skipped `<setenv>` cases.
- Plan and what was done: the parse's global state (`CommandLineGlobalState`) now holds a `ReadEnvironmentVariable` reader, exposed as the internal `CommandLineOptions.ReadEnvironmentVariable` so every `--next` group shares it; `VariableDefinition.Apply` imports `%name` through it. A new public `CommandLineParser.Parse` overload takes the reader after `isWindows`; the existing overloads pass this process's environment (a static field, so no compiler delegate-cache branch), so their behaviour is unchanged. `CurlCommandRunner.ParseCommandLine` passes the runner's `EnvironmentVariables` (the process's in production via `CurlComposition`, none in tests unless given, as every other environment read there).
- Touches widened to `Curl.Console` (the one-argument change in `CurlCommandRunner.ParseCommandLine` is the only way the run's reader reaches the parser); no other task in Doing on origin/work/dark-factory (only BL-1944, Curl.Conformance.*) names it.
- Choice: `WrappedMessage` (`COLUMNS`) left on the process environment. It is a static helper called from dozens of appliers with no options in hand; `COLUMNS` changes only wrapping width and no listed upstream case depends on it, so routing it would widen the task for no case. `DefaultConfigFileSearch` already takes an injected reader; nothing to change.
- 428, 448, 458 not added to `PassingUpstreamCases.txt`: `Curl.Conformance.UnitTests` is outside touches; the next gap run lists them.
- Measured with `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary,Curl.Console` (2026-10-09): no member this task changed fails. Pre-existing failures filed: BL-1945 (Curl.Cli: `RefuseUnlistedName` branch, two complexity-12 members) and BL-1946 (Curl.Console: `CreateDialingSecurityContextFactory` branch, `RemoteHeaderNameStream.ReadLineAsync` complexity 12).
- Tests: `CommandLineVariableOptionTests.Parse_GivenAnEnvironmentReader_ImportsTheVariableThroughItAndNotTheProcessEnvironment` and `Parse_GivenANullEnvironmentReader_ThrowsArgumentNullException`. Fast tests green (Curl.Cli.UnitTests 3885 passed, Curl.Console.UnitTests 2747, Curl.Conformance.UnitTests 1993). The third criterion is ticked for the code this task changed: the library as a whole sits at 99.95% branch because of the pre-existing `RefuseUnlistedName` gap, which is BL-1945's.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. --variable %NAME imports through the run's injected environment reader
