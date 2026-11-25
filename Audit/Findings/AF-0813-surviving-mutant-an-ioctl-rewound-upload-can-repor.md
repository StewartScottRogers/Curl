---
id: AF-0813
title: Surviving mutant: an ioctl-rewound upload can report it was read before any byte was read
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-16_1333.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/IoctlRewoundCallbackReadStream.cs:HasBeenRead-gt:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/IoctlRewoundCallbackReadStream.cs:25:>
task: BL-5267
tasks: BL-5267
found: 2026-11-16
found-at: 74f3f45c47e86300b2b262dc1dacd43b508b09aa
scorecard: 2026-11-16_0514.md
duplicate-of:
closed: 2026-11-16
closed-how: mechanical
closed-by: 2026-11-16_1333.md
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

- 2026-11-16 | 2026-11-16_1333.md | reproduces: no | Ran the -Site command: resolvedLine 25 ('BytesReadSinceRewind > 0' to '>= 0'), outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-16: filed proposed.
- 2026-11-16: accepted -> closed. Re-audit 2026-11-16_1333.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
