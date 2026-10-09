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
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.
