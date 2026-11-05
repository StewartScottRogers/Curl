---
id: BL-4855
title: Fix AF-0667: Surviving mutant: a TLSA query name of exactly 255 octets can be refused as too long
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Dane.UnitLibrary, Curl.Extension.Dane.UnitTests/Wire/DnssecQueryEncoderTests.cs]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4855 — Fix AF-0667: Surviving mutant: a TLSA query name of exactly 255 octets can be refused as too long

## Goal

The defect the audit office reported as AF-0667 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0667 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0667-surviving-mutant-a-tlsa-query-name-of-exactly-255.md`.

Location: `Curl.Extension.Dane.UnitLibrary/Wire/DnssecQueryEncoder.cs:80`

Location: `Curl.Extension.Dane.UnitLibrary/Wire/DnssecQueryEncoder.cs:80`

Mutant 'wire.Count > MaximumNameLength' -> 'wire.Count >= MaximumNameLength' survived (sample -Seed 413592). RFC 1035 allows 255 octets. The tests check only a name over the limit (Encode_Rfc1035Section2_3_4_ANameOver255OctetsIsRefused uses 4x63-octet labels, 257 octets) and never a name of exactly 255. The mutant makes DANE refuse a valid host at the limit, which ends the transfer.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.Dane.UnitLibrary/Wire/DnssecQueryEncoder.cs:80:> -Member EncodeName -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added Encode_Rfc1035Section2_3_4_ANameOfExactly255OctetsIsEncoded to DnssecQueryEncoderTests: labels of 63, 63, 63 and 61 octets, 255 octets with the root. Widened touches to that one test file, which no other task in Doing names. No production code changed, so no quality run was needed.
- Lanes cannot run the audit office's mutation tool, so the mutant was checked by hand: with `wire.Count >= MaximumNameLength` applied, the new test fails, so the mutant is killed; the change was then reverted. The test is platform-neutral.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. A 255-octet TLSA query name is pinned as encoded, killing the >= mutant
