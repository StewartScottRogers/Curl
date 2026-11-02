---
id: BL-4513
title: Fix CI failure ExecuteAsync_NtlmWithServiceNameAndNoCredentialsOffWindows_SendsNoAuthorization on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitTests, Curl.Original.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-11-02
completed:
---
# BL-4513 — Fix CI failure ExecuteAsync_NtlmWithServiceNameAndNoCredentialsOffWindows_SendsNoAuthorization on Linux and macOS

## Goal

`ExecuteAsync_NtlmWithServiceNameAndNoCredentialsOffWindows_SendsNoAuthorization` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ExecuteAsync_NtlmWithServiceNameAndNoCredentialsOffWindows_SendsNoAuthorization` failed on Linux and macOS in CI run 41479689790 (https://github.com/StewartScottRogers/Curl/actions/runs/41479689790). First failing commit: e9a9c5b9.

    Assertion failed. Expected string to not contain the specified substring.

Lanes test only on Windows, so reproduce with `gh run view 41479689790 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `ExecuteAsync_NtlmWithServiceNameAndNoCredentialsOffWindows_SendsNoAuthorization` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-02: Created.
