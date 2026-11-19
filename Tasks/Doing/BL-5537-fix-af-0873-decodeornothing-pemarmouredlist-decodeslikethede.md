---
id: BL-5537
title: Fix AF-0873: DecodeOrNothing_PemArmouredList_DecodesLikeTheDerList only checks both decodes are non-null, never that they match
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests/ServerCertificateVerificationTests.PemRevocationList.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5537 — Fix AF-0873: DecodeOrNothing_PemArmouredList_DecodesLikeTheDerList only checks both decodes are non-null, never that they match

## Goal

The defect the audit office reported as AF-0873 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0873 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitTests/ServerCertificateVerificationTests.PemRevocationList.cs:16`

Location: `Curl.Networking.UnitTests/ServerCertificateVerificationTests.PemRevocationList.cs:16`

The test's only assertions are 'Assert.IsNotNull(ServerCertificateVerification.DecodeOrNothing(pem));' and 'Assert.IsNotNull(ServerCertificateVerification.DecodeOrNothing(der));'. The name promises the PEM-armoured list decodes like the DER list, but the two results are never compared. A PEM path that decoded a different, empty or truncated list would pass. Step 0 candidate kind weak-assertion, only Assert.IsNotNull.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/ServerCertificateVerificationTests.PemRevocationList.cs -SimpleMatch -Pattern 'Assert.IsNotNull(ServerCertificateVerification.DecodeOrNothing(pem));'
```

- Expected: No match: the test compares the PEM decode with the DER decode (issuer, entries, next update).
- Actual: ServerCertificateVerificationTests.PemRevocationList.cs:16:        Assert.IsNotNull(ServerCertificateVerification.DecodeOrNothing(pem));

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
