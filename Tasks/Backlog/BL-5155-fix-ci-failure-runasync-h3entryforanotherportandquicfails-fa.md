---
id: BL-5155
title: Fix CI failure RunAsync_H3EntryForAnotherPortAndQuicFails_FailsWithTheQuicErrorWithoutTryingTcp on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-11-13
completed:
---
# BL-5155 — Fix CI failure RunAsync_H3EntryForAnotherPortAndQuicFails_FailsWithTheQuicErrorWithoutTryingTcp on Linux and macOS

## Goal

`RunAsync_H3EntryForAnotherPortAndQuicFails_FailsWithTheQuicErrorWithoutTryingTcp` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_H3EntryForAnotherPortAndQuicFails_FailsWithTheQuicErrorWithoutTryingTcp` failed on Linux and macOS in CI run 43523918778 (https://github.com/StewartScottRogers/Curl/actions/runs/43523918778). First failing commit: 73f075a3.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 43523918778 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RunAsync_H3EntryForAnotherPortAndQuicFails_FailsWithTheQuicErrorWithoutTryingTcp` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-13: Created.
