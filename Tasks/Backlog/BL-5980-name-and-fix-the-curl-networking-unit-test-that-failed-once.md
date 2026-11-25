---
id: BL-5980
title: Name and fix the Curl.Networking unit test that failed once in a full fast-test run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitTests/TcpConnectorQuicTests.LocalBinding.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5980 — Name and fix the Curl.Networking unit test that failed once in a full fast-test run

## Goal

The `Curl.Networking.UnitTests` test that failed once in a full fast-test run is named and no longer fails under load.

## Context

BL-5978's full fast-test run, made while other lanes were working, reported `Curl.Networking.UnitTests` `Failed: 1, Passed: 4732, Skipped: 33`; the project alone then gave `Passed: 4732, Skipped: 34`. The quiet log did not name the test. The counts suggest one that ends inconclusive when run alone; the port-taking tests in `TcpConnectorQuicTests.LocalBinding.cs` (`Assert.Inconclusive` when a port belongs to another process) are the first suspects. Widen `touches` if it is another file. BL-5978 changed nothing in Curl.Networking.

## Acceptance criteria

- [ ] The Notes name the test and why it failed.
- [ ] The test cannot fail that way again, `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
