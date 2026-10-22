---
id: BL-2937
title: Fix AF-0299: The proxy redial's answersChallenge = true can become false with no test failing, so the reconnect after a closed 407 acts as a first request
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/TcpConnectorProxyRedialTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2937 â€” Fix AF-0299: The proxy redial's answersChallenge = true can become false with no test failing, so the reconnect after a closed 407 acts as a first request

## Goal

The defect the audit office reported as AF-0299 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0299 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0299-the-proxy-redial-s-answerschallenge-true-can-becom.md`.

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:1311`

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:1311`

Sampled mutant (seed 0) survived: answersChallenge = true; => answersChallenge = false; in the loop that dials again after a 407 on a connection the proxy closed ("Connect me again please"). answersChallenge reaches ConnectTunnelVerboseLines.ReportBeforeConnect (PicksDigestBeforeAnyValue, which changes the -v auth line) and ProxyAuthenticator.ContinueAuthorizationAsync(..., !sentAnswersChallenge, ...) (TcpConnector.cs:1820), which changes how the next challenge is answered. No test covers a redial and then a second 407 or its verbose lines.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.cs:1311:true -Member TunnelRequest -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Test-only fix: `ConnectAsync_WithProxyAnyAuth_WhenTheRedialledType1DrawsABareNtlm407_FailsWithThe407`
  (`TcpConnectorProxyRedialTests.cs`) redials after a closing NTLM 407 and answers the redialled
  Type 1 with a bare NTLM 407, which must be curl's `NTLM handshake failure (internal error)`.
  With `answersChallenge = false` at the redial site (now `TcpConnector.cs:1348`, 1311 in the
  finding) the second 407 is taken as a first request and the test fails. Verified by applying
  the mutant by hand (test failed) and restoring it (test passed); a lane may not run the audit
  office's mutation tool. No production code changed, so no Measure-CodeQuality run.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-21: Doing -> Done. Redial test kills the answersChallenge mutant; build and Networking fast tests green
