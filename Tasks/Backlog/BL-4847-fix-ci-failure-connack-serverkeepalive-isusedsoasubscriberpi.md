---
id: BL-4847
title: Fix CI failure Connack_ServerKeepAlive_IsUsedSoASubscriberPings on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4847 — Fix CI failure Connack_ServerKeepAlive_IsUsedSoASubscriberPings on macOS

## Goal

`Connack_ServerKeepAlive_IsUsedSoASubscriberPings` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Connack_ServerKeepAlive_IsUsedSoASubscriberPings` failed on macOS in CI run 41836589655 (https://github.com/StewartScottRogers/Curl/actions/runs/41836589655). First failing commit: 650d20c7.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 41836589655 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Connack_ServerKeepAlive_IsUsedSoASubscriberPings` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-04: Created.
