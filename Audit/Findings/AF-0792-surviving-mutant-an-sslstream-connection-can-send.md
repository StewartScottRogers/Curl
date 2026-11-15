---
id: AF-0792
title: Surviving mutant: an SslStream connection can send a second close_notify after ShutDownSendingAsync
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Networking.UnitLibrary/SslStreamConnection.cs:ShutDownSendingAsync-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/SslStreamConnection.cs:196:true
task: none
tasks:
found: 2026-11-15
found-at: 065474f7431e6647cda727e7f0fd5a25c9859a90
scorecard: 2026-11-15_1159.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0792 - Surviving mutant: an SslStream connection can send a second close_notify after ShutDownSendingAsync

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/SslStreamConnection.cs:196`: Surviving mutant: an SslStream connection can send a second close_notify after ShutDownSendingAsync.

## Evidence

Location: `Curl.Networking.UnitLibrary/SslStreamConnection.cs:196`

Mutant '_tlsEnded = true;' -> '_tlsEnded = false;' in ShutDownSendingAsync survived (seed 5851). The flag tells DisposeAsync that close_notify was already sent. Without it, disposing after ShutDownSendingAsync tries to shut TLS down again over a send side already closed, putting another alert on the wire or failing. The hand-built connection has this test (CloseNotifyOnDisposeTests.DisposeAsync_OfAHandBuiltTlsConnectionAfterSendingWasShutDown_TriesNoSecondCloseNotify); SslStreamConnection has none.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/SslStreamConnection.cs:196:true -Member ShutDownSendingAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-15: filed proposed.
