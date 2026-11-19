---
id: AF-0867
title: Surviving mutant: a SignalR status line of two tokens can be refused
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/HttpResponseReader.cs:ParseStatusLine-lt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/HttpResponseReader.cs:44:<
task: BL-5509
tasks: BL-5509
found: 2026-11-18
found-at: 4f902456d88c3fda856546dbe0c9a7674b1f04c4
scorecard: 2026-11-18_0810.md
duplicate-of:
closed: 2026-11-19
closed-how: mechanical
closed-by: 2026-11-19_0134.md
---
# AF-0867 - Surviving mutant: a SignalR status line of two tokens can be refused

## Summary

High finding from the quality auditor at `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/HttpResponseReader.cs:44`: Surviving mutant: a SignalR status line of two tokens can be refused. Reported by an auditor flagged unreliable in 2026-11-18_0810.md.

## Evidence

Location: `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/HttpResponseReader.cs:44`

Mutant 'if (parts.Length < 2 || ...)' -> 'parts.Length <= 2' survived. A status line with no reason phrase ('HTTP/1.1 200') would throw 'malformed HTTP status line', failing the negotiate or upgrade with an error exit instead of going on. No test sends a two-token status line. This is the same untested boundary AF-0845 found in SocketIoUpgrade.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/HttpResponseReader.cs:44:< -Member ParseStatusLine -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-19 | 2026-11-19_0134.md | reproduces: no | Targeted run Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/HttpResponseReader.cs:44:< (resolvedLine 44) gave outcome killed. This is the rerun result after a worktree-name collision. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-18: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
