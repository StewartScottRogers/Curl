---
id: BL-1090
title: Fix CI failure RunAsync_VerboseMailUploadWithStartTls_WritesTheEstablishedConnectionLineAgainAfterTheUpgrade on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-10-01
completed:
---
# BL-1090 — Fix CI failure RunAsync_VerboseMailUploadWithStartTls_WritesTheEstablishedConnectionLineAgainAfterTheUpgrade on Linux and macOS

## Goal

`RunAsync_VerboseMailUploadWithStartTls_WritesTheEstablishedConnectionLineAgainAfterTheUpgrade` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_VerboseMailUploadWithStartTls_WritesTheEstablishedConnectionLineAgainAfterTheUpgrade` failed on Linux and macOS in CI run 36919676608 (https://github.com/StewartScottRogers/Curl/actions/runs/36919676608). First failing commit: a237e38c.

    Assertion failed. Expected strings to be equal.

Lanes test only on Windows, so reproduce with `gh run view 36919676608 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RunAsync_VerboseMailUploadWithStartTls_WritesTheEstablishedConnectionLineAgainAfterTheUpgrade` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
