---
id: BL-2032
title: Fix CI failure ExecuteAsync_ProxyNtlmPostWithItsType1Message_SendsContentLengthZeroAndNoBody on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitTests, Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-10
completed:
---
# BL-2032 — Fix CI failure ExecuteAsync_ProxyNtlmPostWithItsType1Message_SendsContentLengthZeroAndNoBody on Linux

## Goal

`ExecuteAsync_ProxyNtlmPostWithItsType1Message_SendsContentLengthZeroAndNoBody` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ExecuteAsync_ProxyNtlmPostWithItsType1Message_SendsContentLengthZeroAndNoBody` failed on Linux in CI run 38093700231 (https://github.com/StewartScottRogers/Curl/actions/runs/38093700231). First failing commit: 61e19fdc.

    Assertion failed. Expected collection to contain a specific number of elements.

Lanes test only on Windows, so reproduce with `gh run view 38093700231 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `ExecuteAsync_ProxyNtlmPostWithItsType1Message_SendsContentLengthZeroAndNoBody` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
