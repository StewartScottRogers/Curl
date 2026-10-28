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
completed:
---
# BL-3942 — Fix CI failure ExecuteAsync_LatinOneCredentials_AreSentOneBytePerCharacter on Linux and macOS

## Goal

`ExecuteAsync_LatinOneCredentials_AreSentOneBytePerCharacter` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ExecuteAsync_LatinOneCredentials_AreSentOneBytePerCharacter` failed on Linux and macOS in CI run 40711870444 (https://github.com/StewartScottRogers/Curl/actions/runs/40711870444). First failing commit: 84b0f66b.

    CollectionAssert.AreEqual failed. Element at index 6 do not match.

Lanes test only on Windows, so reproduce with `gh run view 40711870444 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `ExecuteAsync_LatinOneCredentials_AreSentOneBytePerCharacter` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 1777cfb6.
