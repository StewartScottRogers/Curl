---
id: AF-0114
title: WithProxyAuthorization's proxyAuthorizationAnswersChallenge: true can become false with no test failing
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:WithProxyAuthorization-true:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:2838:true
task: none
tasks:
found: 2026-10-08
found-at: cddb276d1d10fbb372f36a32cc1f588fd84c58e8
scorecard: 2026-10-08_2315.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0114 - WithProxyAuthorization's proxyAuthorizationAnswersChallenge: true can become false with no test failing

## Summary

High finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:2838`: WithProxyAuthorization's proxyAuthorizationAnswersChallenge: true can become false with no test failing. Reported by an auditor flagged unreliable in 2026-10-08_2315.md.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:2838`

Mutant 'proxyAuthorizationAnswersChallenge: true' -> 'false' in WithProxyAuthorization (the retry after a 407) survived. The flag feeds HttpAuthUsingLines.AuthUsing for the proxy (line 1484, the verbose 'Proxy auth using ...' line) and AnswerChallengesAsync for the proxy's Proxy-Authenticate challenges (line 2332), which decides whether a multi-leg proxy handshake (NTLM, Negotiate) is continued. The retry after a 407 would then write a different -v line and could end a proxy NTLM handshake after its first leg. No test pins either outcome on the 407 retry.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:2838:true -Member WithProxyAuthorization -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-09 | 2026-10-09_0225.md | reproduces: yes | Ran the -Site reproduction: survived HttpProtocolHandler.cs:2838 true [WithProxyAuthorization] proxyAuthorizationAnswersChallenge: true -> false. It survived in the seed-0 sample as well.

## Log

- 2026-10-08: filed proposed.
- 2026-10-09: proposed -> accepted.
