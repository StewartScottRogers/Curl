---
id: AF-0180
title: HttpContinueWaitConnection.ReadStatusLineAsync's 'IndexOf(...) < 0' can become '<= 0' with no test failing: a reply starting with a bare LF no longer ends the 100-continue wait at once
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-13_1822.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:ReadStatusLineAsync-lt:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:170:<
task: BL-2139
tasks: BL-2139
found: 2026-10-13
found-at: c86d04eeb8477108eb6d9195bb036a4ddaecc5fc
scorecard: 2026-10-13_0925.md
duplicate-of:
closed: 2026-10-13
closed-how: mechanical
closed-by: 2026-10-13_1822.md
---
# AF-0180 - HttpContinueWaitConnection.ReadStatusLineAsync's 'IndexOf(...) < 0' can become '<= 0' with no test failing: a reply starting with a bare LF no longer ends the 100-continue wait at once

## Summary

Medium finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:170`: HttpContinueWaitConnection.ReadStatusLineAsync's 'IndexOf(...) < 0' can become '<= 0' with no test failing: a reply starting with a bare LF no longer ends the 100-continue wait at once. Reported by an auditor flagged unreliable in 2026-10-13_0925.md.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:170`

Sampled mutation (seed 0) of Curl.Protocol.Http.UnitLibrary: 'while ((lineFeed = Array.IndexOf(received, (byte)'\n', 0, receivedLength)) < 0 && receivedLength < received.Length)' mutated to '<= 0' survived. With the original, a first line feed at offset 0 ends the read: StatusCodeOf gives null and the wait learns at once that no 100 Continue came. With the mutant, IndexOf keeps returning 0, so the loop keeps reading until the connection ends or the 16 KB buffer fills. The expect-100 wait therefore runs to its timeout and sends the body to a server that has already answered. That is a decision about which request bytes are sent, and no test feeds a reply whose first byte is a line feed.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:170:< -Member ReadStatusLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-13 | 2026-10-13_1822.md | reproduces: no | Ran Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs:170:< -Member ReadStatusLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600: resolvedLine 170, outcome killed, excludedTests empty. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
- 2026-10-13: accepted -> closed. Re-audit 2026-10-13_1822.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
