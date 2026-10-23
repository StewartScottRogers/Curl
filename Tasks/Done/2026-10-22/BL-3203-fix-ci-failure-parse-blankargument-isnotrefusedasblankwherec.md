---
id: BL-3203
title: Fix CI failure Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitTests, Curl.Cli.UnitLibrary]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3203 — Fix CI failure Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason on Linux and macOS

## Goal

`Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason` failed on Linux and macOS in CI run 39903809116 (https://github.com/StewartScottRogers/Curl/actions/runs/39903809116). First failing commit: 560a15da.

    Assertion failed. Expected collection to not contain the specified element.

Lanes test only on Windows, so reproduce with `gh run view 39903809116 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: only the `ssl-sessions` row failed. The CI log showed Curl on Linux and macOS refusing `--ssl-sessions ''` as blank. That is right for the OpenSSL build, where curl's config2.c reads it with `getstr(&global->ssl_sessions, nextarg, DENY_BLANK)` when TLS session export is built in. The Schannel build has no session export, so it refuses the option as unsupported whatever the value. So the test was wrong, not the parser: it pinned the Windows measurement for every platform.
- Fix (test only, no production change, so no quality run was needed): moved `ssl-sessions` out of `RefusedForAnotherReason` into two pinned tests. One is `[OSCondition(Windows)]`, which checks it is not refused as blank. The other is `[OSCondition(Exclude, Windows)]`, which checks it is refused as blank with exit 2. No real Linux curl could be measured from this Windows lane, so the Linux answer comes from curl's source and Curl's own output in the CI log. Decided by Claude under Stewart's delegation; this follows the existing platform rule, so no ADR.
- CI criterion: the change is platform-neutral and Curl.Cli.UnitTests is green here (4282 passed, 0 failed). The shift's CI watch files a task for any failure.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 315a2185.
- 2026-10-22: Doing -> Done. --ssl-sessions '' is pinned per platform: refused as unsupported on Windows, refused as blank on Linux and macOS
