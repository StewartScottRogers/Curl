---
id: AF-0718
title: Surviving mutant: a hand-built TLS connection can try a second close_notify after ShutDownSendingAsync
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs:ShutDownSendingAsync-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs:114:true
task: BL-5077
tasks: BL-5077
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed: 2026-11-13
closed-how: mechanical
closed-by: 2026-11-13_2321.md
---
# AF-0718 - Surviving mutant: a hand-built TLS connection can try a second close_notify after ShutDownSendingAsync

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs:114`: Surviving mutant: a hand-built TLS connection can try a second close_notify after ShutDownSendingAsync. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs:114`

Mutant '_tlsEnded = true;' -> '_tlsEnded = false;' survived. DisposeAsync's remarks say close_notify is sent 'unless ClearTlsAsync or ShutDownSendingAsync already ended TLS'. With the mutant, DisposeAsync writes another TLS record after the send side was shut down. No test disposes a connection after ShutDownSendingAsync and checks the wire.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs:114:true -Member ShutDownSendingAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-13 | 2026-11-13_2321.md | reproduces: no | Ran the -Site command: outcome killed (_tlsEnded = true -> false). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
- 2026-11-13: accepted -> closed. Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
