---
id: BL-6052
title: Fix CI failure ExecuteAsync_FailRejectsAStreamLeftOpen_CancelsItAndReportsTheConnectionLeftIntact on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitTests, Curl.Original.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-11-26
completed:
---
# BL-6052 — Fix CI failure ExecuteAsync_FailRejectsAStreamLeftOpen_CancelsItAndReportsTheConnectionLeftIntact on Windows

## Goal

`ExecuteAsync_FailRejectsAStreamLeftOpen_CancelsItAndReportsTheConnectionLeftIntact` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ExecuteAsync_FailRejectsAStreamLeftOpen_CancelsItAndReportsTheConnectionLeftIntact` failed on Windows in CI run 45905543947 (https://github.com/StewartScottRogers/Curl/actions/runs/45905543947). First failing commit: e6508e78.

    CollectionAssert.AreEqual failed. Different number of elements.

Failing data rows: `head and DATA, stream left open`.

Lanes test only on Windows, so reproduce with `gh run view 45905543947 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `ExecuteAsync_FailRejectsAStreamLeftOpen_CancelsItAndReportsTheConnectionLeftIntact` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-26: Created.
