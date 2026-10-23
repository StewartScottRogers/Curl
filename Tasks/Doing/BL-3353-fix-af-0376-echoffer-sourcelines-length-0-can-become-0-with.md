---
id: BL-3353
title: Fix AF-0376: EchOffer.SourceLines' Length > 0 can become >= 0 with no test failing, so an empty ECHConfigList from DNS writes the wrong -v ECH lines
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-23
completed:
---
# BL-3353 — Fix AF-0376: EchOffer.SourceLines' Length > 0 can become >= 0 with no test failing, so an empty ECHConfigList from DNS writes the wrong -v ECH lines

## Goal

The defect the audit office reported as AF-0376 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0376 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0376-echoffer-sourcelines-length-0-can-become-0-with-no.md`.

Location: `Curl.Networking.UnitLibrary/EchOffer.cs:92`

Location: `Curl.Networking.UnitLibrary/EchOffer.cs:92`

Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 reported this mutant as survived. Original: : encoded is not { Length: > 0 } ? [NoListLine]; mutated: { Length: >= 0 }. SourceLines writes curl's ECH: setup lines, which go to -v (EchOffer.InfoLines). With the mutant, an HTTPS record whose ech value is present but empty no longer writes NoListLine. It writes DnsListLine plus either 'ECH: imported ECHConfigList of length 0' or DnsListFailedLine. The -v output of an --ech transfer then differs, and no test covers an empty, non-null list from DNS.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/EchOffer.cs:92:> -Member SourceLines -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
