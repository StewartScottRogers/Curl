---
id: AF-0457
title: StreamConnection.HasSocketUnderUnreadBytesOrEnd's || can become && with no test failing, so the check before a write misses a peer that already sent bytes or its FIN
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-31_0358.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/StreamConnection.cs:HasSocketUnderUnreadBytesOrEnd-or:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/StreamConnection.cs:77:||
task: BL-4180
tasks: BL-4180
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed: 2026-10-31
closed-how: mechanical
closed-by: 2026-10-31_0358.md
---
# AF-0457 - StreamConnection.HasSocketUnderUnreadBytesOrEnd's || can become && with no test failing, so the check before a write misses a peer that already sent bytes or its FIN

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/StreamConnection.cs:77`: StreamConnection.HasSocketUnderUnreadBytesOrEnd's || can become && with no test failing, so the check before a write misses a peer that already sent bytes or its FIN. Reported by an auditor flagged unreliable in 2026-10-29_0606.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/StreamConnection.cs:77`

Line 77: `stream is NetworkStream network && (network.Socket.Available > 0 || HasUnreadEnd(network.Socket));`. This is the production check run before each write (line 60, HasUnreadBytesOrEndBeforeWrite). With && it is true only when bytes are waiting and the peer's FIN is also pending, so it almost never fires. A reused connection the server has already closed, or answered early, would then be written to as if live. The mutation run (seed 0) left it surviving. The method is [ExcludeFromCodeCoverage] (`a thin socket adapter, measured by the Integration run`), and the fast tests replace it through the init property, so no unit test ever runs it.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/StreamConnection.cs:77:|| -Member HasSocketUnderUnreadBytesOrEnd -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a loopback test whose peer sends its FIN before a write sees the end reported.
- Actual: outcome survived: every Curl.Networking.UnitTests test passed with the mutant.

## Re-audits

- 2026-10-31 | 2026-10-31_0358.md | reproduces: no | Ran the -Site reproduction: StreamConnection.cs:77 `network.Socket.Available > 0 || HasUnreadEnd(network.Socket)` -> `&&`, outcome killed (resolvedLine 77). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
- 2026-10-31: accepted -> closed. Re-audit 2026-10-31_0358.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
