---
id: BL-3991
title: Fix CI failure -4 on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-28
completed:
---
# BL-3991 — Fix CI failure -4 on Linux and macOS

## Goal

`-4` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `-4` failed on Linux and macOS in CI run 40774593052 (https://github.com/StewartScottRogers/Curl/actions/runs/40774593052). First failing commit: 81764fdf.

    Assertion failed. Expected value to not be null.

Lanes test only on Windows, so reproduce with `gh run view 40774593052 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `-4` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Deferred. Duplicate of BL-3993: the CI watch named a data row ('-4' or '-6') of TcpConnectorTests.ConnectAsync_UnderTheOtherFamilyToAnAddressLiteral_DialsTheLiteral, failing in the same run from the same commit; BL-3993 now covers every row. Revisit only if that test still fails after BL-3993 is Done.
