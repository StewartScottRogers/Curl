---
id: BL-4049
title: Fix flaky CI test AuthenticateAsClientAsync_Tls13InTheOpenSslBuild_ReportsEveryHandshakeMessageInOrder that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-28
completed:
---
# BL-4049 — Fix flaky CI test AuthenticateAsClientAsync_Tls13InTheOpenSslBuild_ReportsEveryHandshakeMessageInOrder that failed once on Windows

## Goal

`AuthenticateAsClientAsync_Tls13InTheOpenSslBuild_ReportsEveryHandshakeMessageInOrder` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `AuthenticateAsClientAsync_Tls13InTheOpenSslBuild_ReportsEveryHandshakeMessageInOrder` failed on Windows in CI run 40824274066 (https://github.com/StewartScottRogers/Curl/actions/runs/40824274066). First failing commit: 0b744431.

    CollectionAssert.AreEqual failed. Different number of elements.

Lanes test only on Windows, so reproduce with `gh run view 40824274066 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] What made `AuthenticateAsClientAsync_Tls13InTheOpenSslBuild_ReportsEveryHandshakeMessageInOrder` fail intermittently is named under Notes and removed.
- [ ] `AuthenticateAsClientAsync_Tls13InTheOpenSslBuild_ReportsEveryHandshakeMessageInOrder` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 946f54b2 c0ca9fbd.
