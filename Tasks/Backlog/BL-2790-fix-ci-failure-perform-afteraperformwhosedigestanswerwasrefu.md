---
id: BL-2790
title: Fix CI failure Perform_AfterAPerformWhoseDigestAnswerWasRefused_SendsTheKeptNonceTwiceOnTheNextPerform on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl.UnitTests, Libcurl]
requirement: none
created: 2026-10-18
completed:
---
# BL-2790 — Fix CI failure Perform_AfterAPerformWhoseDigestAnswerWasRefused_SendsTheKeptNonceTwiceOnTheNextPerform on Linux and macOS

## Goal

`Perform_AfterAPerformWhoseDigestAnswerWasRefused_SendsTheKeptNonceTwiceOnTheNextPerform` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Perform_AfterAPerformWhoseDigestAnswerWasRefused_SendsTheKeptNonceTwiceOnTheNextPerform` failed on Linux and macOS in CI run 39210403935 (https://github.com/StewartScottRogers/Curl/actions/runs/39210403935). First failing commit: 1c3d8cc6.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 39210403935 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Perform_AfterAPerformWhoseDigestAnswerWasRefused_SendsTheKeptNonceTwiceOnTheNextPerform` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-18: Created.
