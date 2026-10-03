---
id: AF-0006
title: DNS label length bound `position + length > message.Length` can become >= with no test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs:DecodeName-gt-boundary:surviving-mutant
task: BL-1262
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed:
closed-by:
---
# AF-0006 - DNS label length bound `position + length > message.Length` can become >= with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs:319`: DNS label length bound `position + length > message.Length` can become >= with no test failing. Reported by an auditor flagged unreliable in 2026-10-02_1400.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs:319`

Mutant survived (seed 0): `>` became `>=` in the check that returns DnsMessageFailure.BadLabel. No test has a label that ends exactly at the end of the message, so the off-by-one is unpinned. This is a refused-input decision on untrusted network data.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-net.json
```

- Expected: The mutant at Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs:319 (>) is killed.
- Actual: survived  Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs:319 >

## Re-audits

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
