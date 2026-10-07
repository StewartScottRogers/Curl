---
id: AF-0046
title: HTTP/2 'SETTINGS received' log condition `!wasSettingsReceived && Frames.IsPeerSettingsReceived` can become || with no test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Protocol.Http.UnitLibrary/Http2Session.cs:LogPeerConnectionFrames-and:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/Http2Session.cs:432:&&
task: none
tasks:
found: 2026-10-07
found-at: 5a627a2fb4baf7b4b2662dc309939ec576dcad20
scorecard: 2026-10-07_0844.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0046 - HTTP/2 'SETTINGS received' log condition `!wasSettingsReceived && Frames.IsPeerSettingsReceived` can become || with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/Http2Session.cs:432`: HTTP/2 'SETTINGS received' log condition `!wasSettingsReceived && Frames.IsPeerSettingsReceived` can become || with no test failing. Reported by an auditor flagged unreliable in 2026-10-07_0844.md.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/Http2Session.cs:432`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Http.UnitLibrary -MaxMutants 40 -Seed 0: 'if (!wasSettingsReceived && Frames.IsPeerSettingsReceived)' -> 'if (!wasSettingsReceived || Frames.IsPeerSettingsReceived)' survived (library score 0.9474). With ||, connectionLog.SettingsReceived writes the server's SETTINGS line before any SETTINGS has arrived, and again after every later frame. No test checks that the line is written exactly once, when the first SETTINGS arrives.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/Http2Session.cs:432:&& -Member LogPeerConnectionFrames -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant at Curl.Protocol.Http.UnitLibrary/Http2Session.cs:432 (&& to ||) is killed.
- Actual: survived  Curl.Protocol.Http.UnitLibrary/Http2Session.cs:432 &&

## Re-audits

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
