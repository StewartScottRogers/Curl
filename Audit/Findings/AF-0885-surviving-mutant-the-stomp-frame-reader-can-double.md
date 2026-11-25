---
id: AF-0885
title: Surviving mutant: the STOMP frame reader can double its buffer on every read and stop growing it when full
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-19_1633.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs:FillAsync-eq:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs:137:==
task: BL-5556
tasks: BL-5556
found: 2026-11-19
found-at: 6e68cb60b626d421113f6d7d7f35c2a46bbc3b46
scorecard: 2026-11-19_0602.md
duplicate-of:
closed: 2026-11-19
closed-how: mechanical
closed-by: 2026-11-19_1633.md
---
# AF-0885 - Surviving mutant: the STOMP frame reader can double its buffer on every read and stop growing it when full

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs:137`: Surviving mutant: the STOMP frame reader can double its buffer on every read and stop growing it when full.

## Evidence

Location: `Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs:137`

Mutant if (end == buffer.Length) -> if (end != buffer.Length) in FillAsync survived all Curl.Extended.Protocol.Stomp.UnitTests (seed 446316). With it, every fill that leaves room doubles the buffer, so a long subscription grows memory exponentially. A buffer that is exactly full is not grown, so ReadAsync gets an empty span, returns 0, and the reader reports the end of the connection inside a frame. ReadAsync_BodyLargerThanTheBuffer_GrowsTheBuffer passes either way, because no test counts the reads or reaches an exactly full buffer.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameReader.cs:137:== -Member FillAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-19 | 2026-11-19_1633.md | reproduces: no | Ran the -Site command: StompFrameReader.cs:137 'if (end == buffer.Length)' -> '!=' at resolvedLine 137, outcome killed (killed=1, survived=0). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-19: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_1633.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
