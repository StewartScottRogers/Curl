---
id: AF-0396
title: DnsServerQuery.QuestionEnd's != 0 can become == 0 with no test failing, so a reply whose question differs after its first five bytes is accepted
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/DnsServerQuery.cs:QuestionEnd-ne:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/DnsServerQuery.cs:121:!=
task: none
tasks:
found: 2026-10-23
found-at: 94da765e1b4eb040511b48ff68f7b42c5226e207
scorecard: 2026-10-23_2322.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0396 - DnsServerQuery.QuestionEnd's != 0 can become == 0 with no test failing, so a reply whose question differs after its first five bytes is accepted

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/DnsServerQuery.cs:121`: DnsServerQuery.QuestionEnd's != 0 can become == 0 with no test failing, so a reply whose question differs after its first five bytes is accepted. Reported by an auditor flagged unreliable in 2026-10-23_2322.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/DnsServerQuery.cs:121`

Sampled mutant (seed 0) survived: 'while (query[index] != 0)' -> 'while (query[index] == 0)'. With the mutant the loop never runs for a real name, QuestionEnd returns HeaderLength + 5, and Match compares only reply[12..17] with the query's question, so a reply naming a different host (or type) after the first label bytes is taken as the answer to the query. No test sends a reply whose question differs past those bytes. This is the reply-matching decision c-ares makes against spoofed or stale answers. (The run left out three ECH tests that fail at baseline; they do not touch DNS.)

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DnsServerQuery.cs:121:!= -Member QuestionEnd -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-23: filed proposed.
- 2026-10-24: proposed -> accepted.
