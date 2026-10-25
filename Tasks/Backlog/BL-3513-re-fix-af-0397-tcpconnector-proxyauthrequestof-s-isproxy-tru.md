---
id: BL-3513
title: Re-fix AF-0397: TcpConnector.ProxyAuthRequestOf's IsProxy: true can become false with no test failing, so proxy Negotiate uses --service-name instead of --proxy-service-name
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3518]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests/TcpConnectorTests.ProxyNtlmAndNegotiate.cs]
requirement: none
created: 2026-10-25
completed:
---
# BL-3513 — Re-fix AF-0397: TcpConnector.ProxyAuthRequestOf's IsProxy: true can become false with no test failing, so proxy Negotiate uses --service-name instead of --proxy-service-name

## Goal

The defect the audit office reported as AF-0397 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0397 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0397-tcpconnector-proxyauthrequestof-s-isproxy-true-can.md`.

Re-fix: the earlier task(s) BL-3444 reached Done, and a later re-audit by the quality auditor found the reproduction still reproduces:

- 2026-10-25 | 2026-10-25_0941.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site reproduction (Curl.Networking.UnitLibrary/TcpConnector.cs:1516:true, -Member ProxyAuthRequestOf): resolvedLine 1516, 'IsProxy: true);' -> 'IsProxy: false);', outcome survived. The same mutant also survived in this audit's seed-0 Networking sample. Runner's targeted mutation rerun on the clean audited commit: survived.

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:1516`

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:1516`

Sampled mutant (seed 0) survived: 'IsProxy: true);' -> 'IsProxy: false);' in the HttpAuthRequest built for the CONNECT tunnel. NegotiateHttpAuthenticator.cs:152 picks the SPN service with request.IsProxy ? options.ProxyServiceName : options.ServiceName, so with the mutant a proxy Negotiate exchange asks for a ticket for the origin's service name and sends a different Proxy-Authorization token - request bytes. No test sets distinct service and proxy service names on a Negotiate tunnel.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.cs:1516:true -Member ProxyAuthRequestOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Why the mutant survives (measured on Windows): with `IsProxy: false` hand-applied at TcpConnector.cs:1516, all `WithProxyNegotiate` tests in Curl.Networking.UnitTests pass. BL-3444's test `ConnectAsync_WithProxyNegotiate_AsksForTheProxyServiceNameNotTheServiceName` checks the service name only through `Diagnostics.Assert("service name", "proxysvc", ...)`, and `TestDiagnostics.Assert` (TestDiagnostics.cs:189) only writes an `ASSERT` log line and never fails. BL-3444's second claim dropped the real `Assert.AreEqual` as a "duplicated service-name assertion". The other two assertions (exit code, request bytes) are the same under both service names because `ScriptedTokenSource` ignores the service name.
- Fix, no production change: in that test, after the `Diagnostics.Assert` line, add `Assert.AreEqual("proxysvc", tokens.Requests.Single().ServiceName);`. Then hand-apply the mutant, confirm the test fails, restore the line, build, and run the fast tests.
- Added `Curl.Networking.UnitTests/TcpConnectorTests.ProxyNtlmAndNegotiate.cs` to touches because the killing assertion has to live in that test. BL-3518 (Doing on origin/work/dark-factory) holds the whole of Curl.Networking.UnitTests, so BL-3518 went into depends-on and the task went back to Backlog. Nothing is left to stash.

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-25: Doing -> Backlog. Needs Curl.Networking.UnitTests/TcpConnectorTests.ProxyNtlmAndNegotiate.cs, which BL-3518 (Doing) holds as the whole Curl.Networking.UnitTests; fix diagnosed in Notes (one real Assert.AreEqual)
