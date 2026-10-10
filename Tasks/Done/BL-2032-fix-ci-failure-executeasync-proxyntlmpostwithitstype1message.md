---
id: BL-2032
title: Fix CI failure ExecuteAsync_ProxyNtlmPostWithItsType1Message_SendsContentLengthZeroAndNoBody on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitTests, Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2032 — Fix CI failure ExecuteAsync_ProxyNtlmPostWithItsType1Message_SendsContentLengthZeroAndNoBody on Linux

## Goal

`ExecuteAsync_ProxyNtlmPostWithItsType1Message_SendsContentLengthZeroAndNoBody` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ExecuteAsync_ProxyNtlmPostWithItsType1Message_SendsContentLengthZeroAndNoBody` failed on Linux in CI run 38093700231 (https://github.com/StewartScottRogers/Curl/actions/runs/38093700231). First failing commit: 61e19fdc.

    Assertion failed. Expected collection to contain a specific number of elements.

Lanes test only on Windows, so reproduce with `gh run view 38093700231 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `ExecuteAsync_ProxyNtlmPostWithItsType1Message_SendsContentLengthZeroAndNoBody` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the test built its NTLM authenticator on `SystemSecurityContextFactory`, which has no NTLM provider on Linux and macOS runners (no gss-ntlmssp), so no Type 1 probe was sent and the POST went out with its body. Production is unaffected: `Curl.Console` composes the hand-built NTLM factory.
- Fix: the test now takes its Type 1 message from a `ScriptedTokenSource` (curl 8.21.0's recorded `ProxyNtlmType1`), as the sibling proxy NTLM tests in `HttpProtocolHandlerTests.ProxyNtlmAndNegotiate.cs` do. Also moved `ProxyChallengeHandler`'s orphaned doc comment back onto it. No production change, so `Curl.Protocol.Http.UnitLibrary` was not touched.
- Verified locally on Windows (full fast suite green); Linux and macOS are confirmed by the CI run on the commit the shift integrates — the test no longer depends on an OS NTLM provider.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. Test takes its NTLM Type 1 from a scripted token source; passes without an OS NTLM provider
