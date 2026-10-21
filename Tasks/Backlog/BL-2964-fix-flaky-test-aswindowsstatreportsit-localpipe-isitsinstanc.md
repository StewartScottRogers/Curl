---
id: BL-2964
title: Fix flaky test AsWindowsStatReportsIt_LocalPipe_IsItsInstanceCount when lanes share the machine
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Core.UnitTests/Multipart/UnseekableFileLengthTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2964 — Fix flaky test AsWindowsStatReportsIt_LocalPipe_IsItsInstanceCount when lanes share the machine

## Goal

`AsWindowsStatReportsIt_LocalPipe_IsItsInstanceCount` passes every time in the fast run, also while other dark factory lanes run tests on the same machine.

## Context

Found by BL-2930's run on lane 9 (2026-10-21): the full fast run failed this one test in `Curl.Core.UnitTests` (`Curl.Core.UnitTests/Multipart/UnseekableFileLengthTests.cs`), 1736 passed, while eight other lanes were building and testing. BL-2930 touched only `Curl.Conformance.LibTest.UnitTests`, so the failure is not its own. A test that reads a named pipe's instance count is likely to depend on the pipe's name being unique to the test run and on nothing else opening it; start by checking how the pipe is named and whether the expected count assumes no other client.

## Acceptance criteria

- [ ] The test names a pipe no other process or test can share, or asserts only what this test itself opened, and the reason is written in the test.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-21: Created.
