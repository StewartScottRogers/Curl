---
id: BL-5085
title: Fix AF-0726: Surviving mutant: a certificate name value with a non-universal or constructed tag can print as text instead of #hex
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary]
requirement: none
created: 2026-11-13
completed:
---
# BL-5085 — Fix AF-0726: Surviving mutant: a certificate name value with a non-universal or constructed tag can print as text instead of #hex

## Goal

The defect the audit office reported as AF-0726 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0726 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0726-surviving-mutant-a-certificate-name-value-with-a-n.md`.

Location: `Curl.Output.UnitLibrary/OpenSslDistinguishedNameText.cs:163`

Location: `Curl.Output.UnitLibrary/OpenSslDistinguishedNameText.cs:163`

Mutant 'TagClass != Universal || IsConstructed || ...' -> 'TagClass != Universal && IsConstructed || ...' survived. A context-specific primitive tag numbered like a string type (12, 19, 22, ...) or a constructed universal string tag is dumped as '#<hex>' by the original and printed as characters by the mutant. That changes the -v certificate subject and issuer lines for such a server certificate. Format_ConstructedValue_DumpsTheDerInHex uses a SEQUENCE (tag 16, not in CharacterWidths), so it cannot tell.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Output.UnitLibrary/OpenSslDistinguishedNameText.cs:163:|| -Member AppendValue -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
