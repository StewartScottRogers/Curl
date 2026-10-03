---
id: BL-1262
title: Fix AF-0006: DNS label length bound `position + length > message.Length` can become >= with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-03
completed:
---
# BL-1262 — Fix AF-0006: DNS label length bound `position + length > message.Length` can become >= with no test failing

## Goal

The defect the audit office reported as AF-0006 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0006 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0006-dns-label-length-bound-position-length-message-len.md`.

Location: `Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs:319`

Location: `Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs:319`

Mutant survived (seed 0): `>` became `>=` in the check that returns DnsMessageFailure.BadLabel. No test has a label that ends exactly at the end of the message, so the off-by-one is unpinned. This is a refused-input decision on untrusted network data.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-net.json
```

- Expected: The mutant at Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs:319 (>) is killed.
- Actual: survived  Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs:319 >

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
