---
id: BL-2173
title: --silent suppresses the Note: messages under --verbose (upstream test1740)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2173 — --silent suppresses the Note: messages under --verbose (upstream test1740)

## Goal

With `--silent`, Curl prints none of the tool's `Note:` messages (such as "Note: Unnecessary use of -X or --request, POST is already inferred.") even under `--verbose`, as curl 8.22.0 does, so upstream test1740 passes.

## Context

Split from BL-2160 (GF-0038, curl 8.22.0 changed behaviour). Measured 2026-10-13 through the ratchet: test1740 ("--silent suppresses Note: messages when verbose") fails on `<verify><stderr>` at byte 0: expected "done", got the Note line. The note is written in `Curl.Console/CurlCommandRunner.cs`; its tests are in `Curl.Console.UnitTests/CurlCommandRunnerCustomRequestNoteTests.cs`. Check every other `Note:` the tool prints for the same rule (curl 8.22.0's `notef` in `src/tool_msgs.c`).

## Acceptance criteria

- [x] test1740 passes and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused unit test in `Curl.Console.UnitTests` pins that `--silent --verbose` prints no Note line (ADR-0482).
- [x] `--ai-help` still describes `--silent` correctly.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.22.0 (mingw, Schannel, `/ucrt64/bin/curl`) on 2026-10-13: `notef` writes nothing under `-s`, with `-S` or not, under `-v` or `--trace-ascii`. Checked for the `-X` note, the `--rate` note, and the default-config-file note (accepted and refused command lines). curl 8.21.0 wrote them all under `-s`.
- One rule for every `Note:` line: `Curl.Console/ToolNotes.AreWritten` (trace on and not `-s`), used by the `--rate`, `--remove-on-error`, `-X`, `--skip-existing`, `--retry` keep/throw-away and `--ssl-sessions` notes. The config-file note is chosen in `Curl.Cli.UnitLibrary/CommandLineParseResult.NotedDefaultConfigFileOf`, so it now returns null under `-s`.
- `touches` widened to Curl.Cli.UnitLibrary and Curl.Cli.UnitTests for that config-file note. No other task in Doing on origin/work/dark-factory named them.
- Tests that pinned 8.21.0's "notes print even with -s" were changed to 8.22.0's answer. New focused tests: `RunAsync_SilentTracedMethodRepeatsTheInferredOne_WritesNoNote` (test1740's command line, ADR-0482), `Parse_SilentVerboseReadWithDefaultConfigFile_NotesNoFile`, `RunAsync_RemoveOnErrorSilentVerbose_PrintsNoNote`. Upstream 994, 996 and 1491 still pass: the harness passes no `-s`.
- No ADR: this matches measured 8.22.0 behaviour under ADR-0479's retarget. `--ai-help` describes `--silent` as "Silent mode" from `CurlHelpTable`, which is still true. Measure-CodeQuality was not run: the only new branch (`-s` in `ToolNotes` and `NotedDefaultConfigFileOf`) is reached both ways by the tests above.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. --silent hides every Note: line under -v or a trace, as curl 8.22.0 does; upstream test1740 passes
