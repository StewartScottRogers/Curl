---
id: BL-4087
title: Test the -Z --fail-early abort delay on a fake TimeProvider
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-4088]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-29
completed:
---
# BL-4087 — Test the -Z --fail-early abort delay on a fake TimeProvider

## Goal

A unit test proves `-Z --fail-early` aborts 1 s after the first failure, however late a second failure comes, using a fake `TimeProvider`.

## Context

Split from BL-4062. Depends on BL-4088, which injects the `TimeProvider` into `ParallelRun`'s abort source. Tests live in `Curl.Console.UnitTests`.

## Acceptance criteria

- [ ] A test in `Curl.Console.UnitTests` uses a fake `TimeProvider`, records two failures 600 ms apart and checks that `IsAborted` is true 1 s after the first.
- [ ] The test does not hang or flake: it passes in 20 consecutive runs of the Console fast tests.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

Split from BL-4062.

## Log

- 2026-10-29: Created.
