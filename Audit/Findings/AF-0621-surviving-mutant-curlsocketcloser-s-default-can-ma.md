---
id: AF-0621
title: Surviving mutant: CurlSocketCloser's default can mark every socket as the connection's own without a test failing
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Engine.UnitLibrary/CurlSocketCloser.cs:CurlSocketCloser-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/CurlSocketCloser.cs:19:false
task: none
tasks:
found: 2026-11-03
found-at: 7410f102c809d5232e4f9b210b3ac32486848c27
scorecard: 2026-11-03_1615.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0621 - Surviving mutant: CurlSocketCloser's default can mark every socket as the connection's own without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/CurlSocketCloser.cs:19`: Surviving mutant: CurlSocketCloser's default can mark every socket as the connection's own without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1615.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/CurlSocketCloser.cs:19`

Mutant 'bool isTheConnectionsOwnSocket = false' -> '= true' survived. Two callers rely on the default: SocketCallbackConnectionListener.cs:63 (an accepted socket with the close-socket callback) and SocketCallbackConnector.cs:116. With the mutant, IsTheConnectionsOwnSocket is true for them, so the close-socket callback runs before the connection closes rather than after (CGR-129). That changes the callback order libcurl callers observe; no test checks it for these two paths.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/CurlSocketCloser.cs:19:false -Member CurlSocketCloser -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
