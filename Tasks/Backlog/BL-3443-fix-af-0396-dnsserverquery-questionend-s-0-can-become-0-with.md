---
id: BL-3443
title: Fix AF-0396: DnsServerQuery.QuestionEnd's != 0 can become == 0 with no test failing, so a reply whose question differs after its first five bytes is accepted
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-24
completed:
---
# BL-3443 — Fix AF-0396: DnsServerQuery.QuestionEnd's != 0 can become == 0 with no test failing, so a reply whose question differs after its first five bytes is accepted

## Goal

The defect the audit office reported as AF-0396 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0396 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0396-dnsserverquery-questionend-s-0-can-become-0-with-n.md`.

Location: `Curl.Networking.UnitLibrary/DnsServerQuery.cs:121`

Location: `Curl.Networking.UnitLibrary/DnsServerQuery.cs:121`

Sampled mutant (seed 0) survived: 'while (query[index] != 0)' -> 'while (query[index] == 0)'. With the mutant the loop never runs for a real name, QuestionEnd returns HeaderLength + 5, and Match compares only reply[12..17] with the query's question, so a reply naming a different host (or type) after the first label bytes is taken as the answer to the query. No test sends a reply whose question differs past those bytes. This is the reply-matching decision c-ares makes against spoofed or stale answers. (The run left out three ECH tests that fail at baseline; they do not touch DNS.)

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DnsServerQuery.cs:121:!= -Member QuestionEnd -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
