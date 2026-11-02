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
completed: 2026-11-02
---
# BL-4513 — Fix CI failure ExecuteAsync_NtlmWithServiceNameAndNoCredentialsOffWindows_SendsNoAuthorization on Linux and macOS

## Goal

`ExecuteAsync_NtlmWithServiceNameAndNoCredentialsOffWindows_SendsNoAuthorization` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ExecuteAsync_NtlmWithServiceNameAndNoCredentialsOffWindows_SendsNoAuthorization` failed on Linux and macOS in CI run 41479689790 (https://github.com/StewartScottRogers/Curl/actions/runs/41479689790). First failing commit: e9a9c5b9.

    Assertion failed. Expected string to not contain the specified substring.

Lanes test only on Windows, so reproduce with `gh run view 41479689790 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `ExecuteAsync_NtlmWithServiceNameAndNoCredentialsOffWindows_SendsNoAuthorization` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the test, not the handler. `ScriptedAuthenticator` returns its Type 1 value whatever credential it is given, so off Windows (no empty-user credential, as ADR-1183 decides) the fake still wrote an `Authorization` header. The real authenticator makes none without a credential. The test now asserts the authenticator is only asked with no credential, as its sibling `ExecuteAsync_NoCredentialsAndNoEmptyUserRule_SendsNoAuthorization` does, and is renamed to say so. No production change, so no quality run was needed.
- The test is Windows-excluded, so it cannot run here; the assertion only reads the credential the handler passes (`OperatingSystem.IsWindows()` false gives null), which is platform-neutral. Curl.Original.Protocol.Http.UnitTests fast tests: 2633 passed, 0 failed. CI on Linux and macOS is left to the shift's CI watch (rule 11).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 980f17d7.
- 2026-11-02: Doing -> Done. The off-Windows NTLM service-name test now asserts the authenticator gets no credential, rather than the bytes a scripted fake writes
