---
id: BL-5844
title: Fix CI failure VerifyPeer_ExcludedDirectoryNameWithANonStringValue_DoesNotMatchAStringSubject on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-23
completed:
---
# BL-5844 — Fix CI failure VerifyPeer_ExcludedDirectoryNameWithANonStringValue_DoesNotMatchAStringSubject on Linux

## Goal

`VerifyPeer_ExcludedDirectoryNameWithANonStringValue_DoesNotMatchAStringSubject` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `VerifyPeer_ExcludedDirectoryNameWithANonStringValue_DoesNotMatchAStringSubject` failed on Linux in CI run 45256967525 (https://github.com/StewartScottRogers/Curl/actions/runs/45256967525). First failing commit: 61420aa0.

    Assertion failed. Expected value to be null.

Lanes test only on Windows, so reproduce with `gh run view 45256967525 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `VerifyPeer_ExcludedDirectoryNameWithANonStringValue_DoesNotMatchAStringSubject` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-23: Created.
