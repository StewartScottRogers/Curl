---
id: BL-2707
title: Fix CI failure ListenAsync_WhenTheFirstPortIsTaken_ListensOnTheNextFreeOne on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-17
completed:
---
# BL-2707 — Fix CI failure ListenAsync_WhenTheFirstPortIsTaken_ListensOnTheNextFreeOne on Windows

## Goal

`ListenAsync_WhenTheFirstPortIsTaken_ListensOnTheNextFreeOne` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ListenAsync_WhenTheFirstPortIsTaken_ListensOnTheNextFreeOne` failed on Windows in CI run 39091460067 (https://github.com/StewartScottRogers/Curl/actions/runs/39091460067). First failing commit: 1c266d83.

    Test method Curl.Networking.TcpConnectionListenerTests.ListenAsync_WhenTheFirstPortIsTaken_ListensOnTheNextFreeOne threw exception:

Lanes test only on Windows, so reproduce with `gh run view 39091460067 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `ListenAsync_WhenTheFirstPortIsTaken_ListensOnTheNextFreeOne` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 7e91f4bf.
