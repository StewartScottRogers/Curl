---
id: BL-3070
title: Fix AF-0317: DnsServerQuery.FoldAsciiLetter's <= 'Z' can become < 'Z' with no test failing, so a --dns-servers reply whose echoed question differs only in the case of Z is rejected
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-22
completed:
---
# BL-3070 — Fix AF-0317: DnsServerQuery.FoldAsciiLetter's <= 'Z' can become < 'Z' with no test failing, so a --dns-servers reply whose echoed question differs only in the case of Z is rejected

## Goal

The defect the audit office reported as AF-0317 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0317 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0317-dnsserverquery-foldasciiletter-s-z-can-become-z-wi.md`.

Location: `Curl.Networking.UnitLibrary/DnsServerQuery.cs:143`

Location: `Curl.Networking.UnitLibrary/DnsServerQuery.cs:143`

Sampled mutant (seed 0) survived: 'value is >= (byte)'A' and <= (byte)'Z' ? (byte)(value | 0x20) : value' with <= turned into <. FoldAsciiLetter feeds AsciiEqualsIgnoringCase, which line 68 uses to match a reply's question section against the query. Under the mutant, 'Z' is not folded, so a reply that echoes a name with 'z' for 'Z' (or the reverse) no longer matches and is dropped. The lookup then times out or fails with exit 6. No test pins case-insensitive matching on the last letter.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DnsServerQuery.cs:143:<= -Member FoldAsciiLetter -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
