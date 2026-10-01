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
completed: 2026-09-30
---
# BL-1044 — Fix CI failure CreateRunner_SftpDownloadWithAPassword_WritesCurlsVerboseLines on Linux and macOS

## Goal

`CreateRunner_SftpDownloadWithAPassword_WritesCurlsVerboseLines` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `CreateRunner_SftpDownloadWithAPassword_WritesCurlsVerboseLines` failed on Linux and macOS in CI run 36761924824 (https://github.com/StewartScottRogers/Curl/actions/runs/36761924824). First failing commit: 9691137a.

    Assertion failed. Expected strings to be equal.

Lanes test only on Windows, so reproduce with `gh run view 36761924824 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `CreateRunner_SftpDownloadWithAPassword_WritesCurlsVerboseLines` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the test pinned WinCNG's denial reason, `Reason unknown (-1)`, on every platform, but off Windows the composition runs the OpenSSL backend, which (BL-990, `SshUserAuthentication.UnreadablePublicKeyReasonAsync`) reports `Unable to extract public key from private key file: Unable to open private key file` for the missing `--key` file. Production is right; the test now picks the reason by platform, as it already did for the backend line. CI run 36761924824 shows exactly that string as the actual value on Linux and macOS.
- Passes locally on Windows (Curl.Console.UnitTests 1923 passed; all fast tests green). A lane cannot push, so CI on the landing commit is confirmed by the shift's integration; the Linux/macOS expectation is the string CI itself produced.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. SFTP password -v test expects OpenSSL's missing-key denial reason off Windows, WinCNG's on Windows
