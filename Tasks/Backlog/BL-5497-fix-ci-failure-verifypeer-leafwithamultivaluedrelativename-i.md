---
id: BL-5497
title: Fix CI failure VerifyPeer_LeafWithAMultiValuedRelativeName_IgnoresIt on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-18
completed:
---
# BL-5497 — Fix CI failure VerifyPeer_LeafWithAMultiValuedRelativeName_IgnoresIt on Linux

## Goal

`VerifyPeer_LeafWithAMultiValuedRelativeName_IgnoresIt` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `VerifyPeer_LeafWithAMultiValuedRelativeName_IgnoresIt` failed on Linux in CI run 44362578598 (https://github.com/StewartScottRogers/Curl/actions/runs/44362578598). First failing commit: 183268fd.

    Assertion failed. Expected value to be null.

Lanes test only on Windows, so reproduce with `gh run view 44362578598 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `VerifyPeer_LeafWithAMultiValuedRelativeName_IgnoresIt` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-18: Created.
