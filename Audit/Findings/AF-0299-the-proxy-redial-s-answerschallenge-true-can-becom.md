---
id: AF-0299
title: The proxy redial's answersChallenge = true can become false with no test failing, so the reconnect after a closed 407 acts as a first request
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-22_0130.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/TcpConnector.cs:TunnelRequest-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TcpConnector.cs:1311:true
task: BL-2937
tasks: BL-2937
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed: 2026-10-22
closed-how: mechanical
closed-by: 2026-10-22_0130.md
---
# AF-0299 - The proxy redial's answersChallenge = true can become false with no test failing, so the reconnect after a closed 407 acts as a first request

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/TcpConnector.cs:1311`: The proxy redial's answersChallenge = true can become false with no test failing, so the reconnect after a closed 407 acts as a first request.

## Evidence

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:1311`

Sampled mutant (seed 0) survived: answersChallenge = true; => answersChallenge = false; in the loop that dials again after a 407 on a connection the proxy closed ("Connect me again please"). answersChallenge reaches ConnectTunnelVerboseLines.ReportBeforeConnect (PicksDigestBeforeAnyValue, which changes the -v auth line) and ProxyAuthenticator.ContinueAuthorizationAsync(..., !sentAnswersChallenge, ...) (TcpConnector.cs:1820), which changes how the next challenge is answered. No test covers a redial and then a second 407 or its verbose lines.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.cs:1311:true -Member TunnelRequest -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-22 | 2026-10-22_0130.md | reproduces: no | Ran the -Site reproduction: the site moved; resolved within TunnelRequest to line 1376, mutant Curl.Networking.UnitLibrary/TcpConnector.cs:1376:true killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-19: filed proposed.
- 2026-10-21: proposed -> accepted.
- 2026-10-22: accepted -> closed. Re-audit 2026-10-22_0130.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
