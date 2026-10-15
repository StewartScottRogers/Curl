---
id: AF-0218
title: HttpLineReader.ReadLineAsync's 'lineFeed >= 0' can become 'lineFeed > 0' with no test failing
auditor: quality
severity: Medium
status: accepted
reason: Stewart accepted it on 2026-10-14.
key: quality:Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs:ReadLineAsync-ge:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs:70:>=
task: BL-2255
tasks: BL-2255
found: 2026-10-14
found-at: d86a80f0091fbf88c08d7b9b2fa37a9197854bd8
scorecard: 2026-10-14_1815.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0218 - HttpLineReader.ReadLineAsync's 'lineFeed >= 0' can become 'lineFeed > 0' with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs:70`: HttpLineReader.ReadLineAsync's 'lineFeed >= 0' can become 'lineFeed > 0' with no test failing. Reported by an auditor flagged unreliable in 2026-10-14_1815.md.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs:70`

Sampled run (seed 0) of Curl.Protocol.Http.UnitLibrary: mutant 'if (lineFeed >= 0)' -> 'if (lineFeed > 0)' in ReadLineAsync survived. lineFeed is an absolute buffer index, so it is 0 when a line starts at buffer[0] and is a bare LF. That happens for a response whose first byte is LF, or for an LF-only blank line left at the front after MakeRoom compacts a full buffer. Under the mutant that line is not returned: it is run through RejectHttp09 as a status line, or merged with the next line, so the head ends in the wrong place. No test puts a lone LF at buffer index 0.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs:70:>= -Member ReadLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-14: filed proposed.
- 2026-10-14: proposed -> accepted.
