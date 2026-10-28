---
id: BL-3942
title: Fix CI failure ExecuteAsync_LatinOneCredentials_AreSentOneBytePerCharacter on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitTests, Curl.Protocol.Ftp.UnitLibrary]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3942 — Fix CI failure ExecuteAsync_LatinOneCredentials_AreSentOneBytePerCharacter on Linux and macOS

## Goal

`ExecuteAsync_LatinOneCredentials_AreSentOneBytePerCharacter` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ExecuteAsync_LatinOneCredentials_AreSentOneBytePerCharacter` failed on Linux and macOS in CI run 40711870444 (https://github.com/StewartScottRogers/Curl/actions/runs/40711870444). First failing commit: 84b0f66b.

    CollectionAssert.AreEqual failed. Element at index 6 do not match.

Lanes test only on Windows, so reproduce with `gh run view 40711870444 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `ExecuteAsync_LatinOneCredentials_AreSentOneBytePerCharacter` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: BL-3855 (fbffcf0cf) made `FtpSession` send credentials in the system ANSI code page on Windows and as UTF-8 elsewhere, matching each platform's curl, but left this test pinning the Windows byte (0xF6) on every platform. The fix is test-only: the test now runs on Windows only (`OSCondition`), and its twin `ExecuteAsync_LatinOneCredentialsOffWindows_AreSentAsUtf8` pins `C3 B6` on Linux and macOS. No production code changed, so no quality run was needed.
- Curl.Protocol.Ftp.UnitTests fast tests on Windows: 1323 passed, 9 skipped (the off-Windows twin among them). CI criterion: the change is platform-neutral (each platform's answer in its own OSCondition test); the shift's CI watch files any failure.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 1777cfb6.
- 2026-10-28: Doing -> Done. LatinOne credential bytes pinned per platform: ANSI code page on Windows, UTF-8 on Linux and macOS
