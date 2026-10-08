---
id: BL-1759
title: Fix AF-0081: OpenCertificates_WithTheMyStoreOnWindows_ReturnsItsCertificates checks only that the result is not null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1759 — Fix AF-0081: OpenCertificates_WithTheMyStoreOnWindows_ReturnsItsCertificates checks only that the result is not null

## Goal

The defect the audit office reported as AF-0081 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0081 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0081-opencertificates-withthemystoreonwindows-returnsit.md`.

Location: `Curl.Networking.UnitTests/SystemClientCertificateStoreTests.cs:29`

Location: `Curl.Networking.UnitTests/SystemClientCertificateStoreTests.cs:29`

The only assertion is 'Assert.IsNotNull(certificates);'. The name promises the store's certificates, but an implementation that always returned an empty collection would pass. The test could compare the result with what X509Store(StoreName.My, location) itself lists.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/SystemClientCertificateStoreTests.cs -Pattern 'Assert\.'
```

- Expected: An assertion comparing the returned certificates with the store's own contents.
- Actual: SystemClientCertificateStoreTests.cs:29: Assert.IsNotNull(certificates);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
