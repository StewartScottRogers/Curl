---
id: BL-2155
title: Run and debug one upstream case by number, keeping its logs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2155 — Run and debug one upstream case by number, keeping its logs

## Goal

A developer can run any single upstream case by its number, whether or not it is in `PassingUpstreamCases.txt`, from Test Explorer or one command line, and keep its log folder (stdout, stderr, the server's protocol dump, the trace) to look at afterwards.

## Context

The second of the three fixes Stewart chose on 2026-10-13 instead of converting the vendored upstream cases to hand-written C# tests. Today only the cases in `PassingUpstreamCases.txt` run as rows, and each case's log folder (`bin/.../log/test<N>-<guid>`) is deleted or hard to find. The gap office's `Gap/Tools/Measure-UpstreamCases.cs` has a `--keep-logs` mode, but lanes cannot use `Gap/` paths, so the in-repo harness needs its own way.

## Acceptance criteria

- [x] An explicit, not-run-by-default test (for example `[TestCategory("Manual")]` driven by an environment variable such as `CURL_UPSTREAM_CASE=798`) runs the named case and fails with the same readable message as the ratchet.
- [x] With `CURL_UPSTREAM_KEEP_LOGS=1`, any upstream case run keeps its log folder and prints its path.
- [x] `Curl.Conformance.UnitTests/CLAUDE.md` (create it if absent) gives the exact command lines for both, and the fast test command still skips the manual test.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

- `UpstreamCaseDebugSettings` (library) reads `CURL_UPSTREAM_CASE` and `CURL_UPSTREAM_KEEP_LOGS` through an injected reader, so it is unit tested and held to the gates; `UpstreamCaseDebugSettingsTests` cover every branch.
- The manual test `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` carries `TestCategory("Manual")` and `RunsOnlyWhenAnUpstreamCaseIsNamed`, a `ConditionBaseAttribute` like the long-running one, so without the variable the fast run reports it skipped. Choice: it judges the case with its real listing and fails unless it passes, so an unlisted case's message reads `test<N> fails: ...` rather than claiming it is listed; a listed case gets the ratchet's own message.
- `CURL_UPSTREAM_KEEP_LOGS=1` keeps the folder in `DeleteOrKeepLogDirectory` (renamed from `DeleteLogDirectory`) and prints `Kept the case's log folder: <path>` to the test's standard output.
- Measured: case 798 passes and keeps its folder; listed case 8 fails with the ratchet's message, first difference and line diff; `--filter "Name~test798 "` selects the ratchet row.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. CURL_UPSTREAM_CASE runs any upstream case by number in a Manual test, and CURL_UPSTREAM_KEEP_LOGS=1 keeps and prints each case's log folder
