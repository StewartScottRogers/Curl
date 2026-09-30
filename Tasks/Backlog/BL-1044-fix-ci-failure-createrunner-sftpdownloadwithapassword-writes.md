---
id: BL-1044
title: Fix CI failure CreateRunner_SftpDownloadWithAPassword_WritesCurlsVerboseLines on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-09-30
completed:
---
# BL-1044 — Fix CI failure CreateRunner_SftpDownloadWithAPassword_WritesCurlsVerboseLines on Linux and macOS

## Goal

`CreateRunner_SftpDownloadWithAPassword_WritesCurlsVerboseLines` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `CreateRunner_SftpDownloadWithAPassword_WritesCurlsVerboseLines` failed on Linux and macOS in CI run 36761924824 (https://github.com/StewartScottRogers/Curl/actions/runs/36761924824). First failing commit: 9691137a.

    Assertion failed. Expected strings to be equal.

Lanes test only on Windows, so reproduce with `gh run view 36761924824 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `CreateRunner_SftpDownloadWithAPassword_WritesCurlsVerboseLines` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-09-30: Created.
