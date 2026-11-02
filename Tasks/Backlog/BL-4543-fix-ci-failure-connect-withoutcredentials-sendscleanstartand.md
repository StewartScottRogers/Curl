---
id: BL-4543
title: Fix CI failure Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst on Linux and macOS and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-02
completed:
---
# BL-4543 — Fix CI failure Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst on Linux and macOS and Windows

## Goal

`Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst` failed on Linux and macOS and Windows in CI run 41516637939 (https://github.com/StewartScottRogers/Curl/actions/runs/41516637939). First failing commit: 6ada3366.

    CollectionAssert.AreEqual failed. Different number of elements.

Lanes test only on Windows, so reproduce with `gh run view 41516637939 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-02: Created.
