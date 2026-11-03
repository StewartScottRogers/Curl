---
id: BL-3992
title: Fix CI failure -6 on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-28
completed: 2026-11-02
---
# BL-3992 — Fix CI failure -6 on Linux and macOS

## Goal

`-6` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `-6` failed on Linux and macOS in CI run 40774593052 (https://github.com/StewartScottRogers/Curl/actions/runs/40774593052). First failing commit: 81764fdf.

    Assertion failed. Expected value to not be null.

Lanes test only on Windows, so reproduce with `gh run view 40774593052 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `-6` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- 2026-11-02 (lane 3): No code change needed. BL-3993 (Done, aefae1ea4) pinned every -4/-6 row of TcpConnectorTests.ConnectAsync_UnderTheOtherFamilyToAnAddressLiteral_DialsTheLiteral to the Schannel build connector on every platform. Locally all 3 rows pass (Curl.Networking.UnitTests filter, 3 passed). CI on work/dark-factory passed on Windows, Linux and macOS in run 41467980936 (2026-11-02), and the one failing run since then (41465596578) does not name this test.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Deferred. Duplicate of BL-3993: the CI watch named a data row ('-4' or '-6') of TcpConnectorTests.ConnectAsync_UnderTheOtherFamilyToAnAddressLiteral_DialsTheLiteral, failing in the same run from the same commit; BL-3993 now covers every row. Revisit only if that test still fails after BL-3993 is Done.
- 2026-11-02: Deferred -> Backlog. Stewart 2026-11-02: complete all existing tasks before the third-party handoff.
- 2026-11-02: Backlog -> Doing. Criteria at claim: e4bd77f6.
- 2026-11-02: Doing -> Done. -6 row passes locally and on Linux/macOS CI since BL-3993's fix; no further change needed
