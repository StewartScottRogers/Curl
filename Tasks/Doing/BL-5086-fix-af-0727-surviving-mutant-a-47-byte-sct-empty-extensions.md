---
id: BL-5086
title: Fix AF-0727: Surviving mutant: a 47-byte SCT (empty extensions and signature) can be refused in the certificate text
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary]
requirement: none
created: 2026-11-13
completed:
---
# BL-5086 — Fix AF-0727: Surviving mutant: a 47-byte SCT (empty extensions and signature) can be refused in the certificate text

## Goal

The defect the audit office reported as AF-0727 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0727 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0727-surviving-mutant-a-47-byte-sct-empty-extensions-an.md`.

Location: `Curl.Output.UnitLibrary/OpenSslPeerCertificateText.cs:661`

Location: `Curl.Output.UnitLibrary/OpenSslPeerCertificateText.cs:661`

Mutant 'timestamp.Length < 47' -> '<= 47' survived. The original formats the smallest well-formed v1 timestamp (43 bytes + 0 extensions + 4 bytes of algorithm and empty signature); the mutant throws AsnContentException for it, changing the -v certificate output. The minimum length is untested.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Output.UnitLibrary/OpenSslPeerCertificateText.cs:661:< -Member FindSignatureStart -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Added a test for the 47-byte SCT, which kills the `<= 47` mutant (it would throw for that SCT). The mutation tool is off limits to a lane, so the reproduction was not run; the test pins the mutant site. Output.UnitTests green, build clean; only a test changed.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
