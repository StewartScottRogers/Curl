---
id: BL-2173
title: --silent suppresses the Note: messages under --verbose (upstream test1740)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-13
completed:
---
# BL-2173 — --silent suppresses the Note: messages under --verbose (upstream test1740)

## Goal

With `--silent`, Curl prints none of the tool's `Note:` messages (such as "Note: Unnecessary use of -X or --request, POST is already inferred.") even under `--verbose`, as curl 8.22.0 does, so upstream test1740 passes.

## Context

Split from BL-2160 (GF-0038, curl 8.22.0 changed behaviour). Measured 2026-10-13 through the ratchet: test1740 ("--silent suppresses Note: messages when verbose") fails on `<verify><stderr>` at byte 0: expected "done", got the Note line. The note is written in `Curl.Console/CurlCommandRunner.cs`; its tests are in `Curl.Console.UnitTests/CurlCommandRunnerCustomRequestNoteTests.cs`. Check every other `Note:` the tool prints for the same rule (curl 8.22.0's `notef` in `src/tool_msgs.c`).

## Acceptance criteria

- [ ] test1740 passes and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in `Curl.Console.UnitTests` pins that `--silent --verbose` prints no Note line (ADR-0482).
- [ ] `--ai-help` still describes `--silent` correctly.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-13: Created.
