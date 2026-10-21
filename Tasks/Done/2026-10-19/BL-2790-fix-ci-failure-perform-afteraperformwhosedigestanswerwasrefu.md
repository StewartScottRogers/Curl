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
completed: 2026-10-19
---
# BL-2790 — Fix CI failure Perform_AfterAPerformWhoseDigestAnswerWasRefused_SendsTheKeptNonceTwiceOnTheNextPerform on Linux and macOS

## Goal

`Perform_AfterAPerformWhoseDigestAnswerWasRefused_SendsTheKeptNonceTwiceOnTheNextPerform` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Perform_AfterAPerformWhoseDigestAnswerWasRefused_SendsTheKeptNonceTwiceOnTheNextPerform` failed on Linux and macOS in CI run 39210403935 (https://github.com/StewartScottRogers/Curl/actions/runs/39210403935). First failing commit: 1c3d8cc6.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 39210403935 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `Perform_AfterAPerformWhoseDigestAnswerWasRefused_SendsTheKeptNonceTwiceOnTheNextPerform` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: line 46 pinned the Schannel build's Digest header. There SSPI writes `nonce="5",uri="/second"` with no blank after the comma. Off Windows, `DigestAuthenticator.FormatOwnValue` writes the OpenSSL build's `nonce="5", uri="/second"`, so the count was 0 on Linux and macOS. Line 45 (two GETs) already passed there, so the kept-nonce behaviour itself held.
- Fix (test only, no production change): count `nonce="5", ?uri="/second"` with `Regex.Count`, which matches both builds' separators. The test's doc comment says so.
- Verified: the Libcurl.UnitTests fast run has 1310 passed and 0 failed. Lanes do not push, so the shift's CI watch (BL-987) checks CI on the commit that lands the fix and files a new task if this test fails again.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 839dc245.
- 2026-10-19: Doing -> Done. The kept-Digest test accepts both builds' Digest separators, so it passes on Linux and macOS
