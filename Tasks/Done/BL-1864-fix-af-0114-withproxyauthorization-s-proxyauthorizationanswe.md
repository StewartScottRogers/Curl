---
id: BL-1864
title: Fix AF-0114: WithProxyAuthorization's proxyAuthorizationAnswersChallenge: true can become false with no test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1864 — Fix AF-0114: WithProxyAuthorization's proxyAuthorizationAnswersChallenge: true can become false with no test failing

## Goal

The defect the audit office reported as AF-0114 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0114 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0114-withproxyauthorization-s-proxyauthorizationanswers.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:2838`

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:2838`

Mutant 'proxyAuthorizationAnswersChallenge: true' -> 'false' in WithProxyAuthorization (the retry after a 407) survived. The flag feeds HttpAuthUsingLines.AuthUsing for the proxy (line 1484, the verbose 'Proxy auth using ...' line) and AnswerChallengesAsync for the proxy's Proxy-Authenticate challenges (line 2332), which decides whether a multi-leg proxy handshake (NTLM, Negotiate) is continued. The retry after a 407 would then write a different -v line and could end a proxy NTLM handshake after its first leg. No test pins either outcome on the 407 retry.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:2838:true -Member WithProxyAuthorization -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- New test `ExecuteAsync_ProxyHandshakeContinuedTwice_TellsTheSecondContinuationItsValueAnsweredAChallenge`
  (`Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.ProxyNtlmAndNegotiate.cs`), the proxy
  mirror of the origin's `ExecuteAsync_HandshakeContinuedTwice_...`: two 407s then 200 through
  `HandshakeAuthenticator`; the second continuation must be told its value answered a
  challenge (`sentBeforeAnyChallenge: false`). That is the flag's effect through
  `AnswerChallengesAsync`. The `Proxy auth using` line does not depend on the flag for a
  value that names its scheme, so the continuation is the observable pinned.
- Mutant checked by hand (lanes may not run `Audit/Tools`): flipping line 2838 to `false`
  fails the new test at index 1 (expected False); reverted, it passes. No production change
  was needed; `touches` covers the library, the test sits in its `.UnitTests` project.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. A proxy handshake test now pins that the retry after a 407 answers a challenge; the AF-0114 mutant fails it.
