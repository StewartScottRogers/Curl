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
completed:
---
# BL-2155 — Run and debug one upstream case by number, keeping its logs

## Goal

A developer can run any single upstream case by its number, whether or not it is in `PassingUpstreamCases.txt`, from Test Explorer or one command line, and keep its log folder (stdout, stderr, the server's protocol dump, the trace) to look at afterwards.

## Context

The second of the three fixes Stewart chose on 2026-10-13 instead of converting the `.rawhttp` cases to hand-written C# tests. Today only the cases in `PassingUpstreamCases.txt` run as rows, and each case's log folder (`bin/.../log/test<N>-<guid>`) is deleted or hard to find. The gap office's `Gap/Tools/Measure-UpstreamCases.cs` has a `--keep-logs` mode, but lanes cannot use `Gap/` paths, so the in-repo harness needs its own way.

## Acceptance criteria

- [ ] An explicit, not-run-by-default test (for example `[TestCategory("Manual")]` driven by an environment variable such as `CURL_UPSTREAM_CASE=798`) runs the named case and fails with the same readable message as the ratchet.
- [ ] With `CURL_UPSTREAM_KEEP_LOGS=1`, any upstream case run keeps its log folder and prints its path.
- [ ] `Curl.Conformance.UnitTests/CLAUDE.md` (create it if absent) gives the exact command lines for both, and the fast test command still skips the manual test.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

## Log

- 2026-10-13: Created.
