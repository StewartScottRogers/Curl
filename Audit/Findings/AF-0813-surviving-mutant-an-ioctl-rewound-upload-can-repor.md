---
id: AF-0813
title: Surviving mutant: an ioctl-rewound upload can report it was read before any byte was read
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/IoctlRewoundCallbackReadStream.cs:HasBeenRead-gt:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/IoctlRewoundCallbackReadStream.cs:25:>
task: none
tasks:
found: 2026-11-16
found-at: 74f3f45c47e86300b2b262dc1dacd43b508b09aa
scorecard: 2026-11-16_0514.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0813 - Surviving mutant: an ioctl-rewound upload can report it was read before any byte was read

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/IoctlRewoundCallbackReadStream.cs:25`: Surviving mutant: an ioctl-rewound upload can report it was read before any byte was read.

## Evidence

Location: `Curl.Engine.UnitLibrary/IoctlRewoundCallbackReadStream.cs:25`

`BytesReadSinceRewind > 0` changed to `>= 0` survived, so HasBeenRead is always true. That decides whether the engine calls CURLOPT_IOCTLFUNCTION's RESTARTREAD before re-sending a body, so a redirect or auth retry with nothing read would still call the application's ioctl callback (and fail if it refuses). No test covers the not-yet-read case.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/IoctlRewoundCallbackReadStream.cs:25:> -Member HasBeenRead -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-16: filed proposed.
