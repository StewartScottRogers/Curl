---
id: AF-0047
title: AbandonResponseAsync's `isResponseEnded = true` can become false with no test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs:AbandonResponseAsync-true:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs:211:true
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
# AF-0047 - AbandonResponseAsync's `isResponseEnded = true` can become false with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs:211`: AbandonResponseAsync's `isResponseEnded = true` can become false with no test failing. Reported by an auditor flagged unreliable in 2026-10-07_0844.md.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs:211`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Http.UnitLibrary -MaxMutants 40 -Seed 0: 'isResponseEnded = true;' -> 'isResponseEnded = false;' survived. The doc comment says the stream is reset with STREAM_CLOSED once and 'the frames the peer still sends on it are dropped'. With the mutant the stream never counts as ended: a second AbandonResponseAsync writes a second RST_STREAM (extra request bytes), and the reads at lines 112, 171 and 260 keep waiting for or taking frames on the reset stream instead of dropping them. No test abandons twice or reads after abandoning.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs:211:true -Member AbandonResponseAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant at Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs:211 (true to false) is killed.
- Actual: survived  Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs:211 true

## Re-audits

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
