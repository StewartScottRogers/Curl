---
id: BL-2188
title: Fix AF-0195: AutomaticClientCertificate.Qualifies' expiry check can become exclusive with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-14
completed:
---
# BL-2188 — Fix AF-0195: AutomaticClientCertificate.Qualifies' expiry check can become exclusive with no test failing

## Goal

The defect the audit office reported as AF-0195 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0195 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0195-automaticclientcertificate-qualifies-expiry-check.md`.

Location: `Curl.Networking.UnitLibrary/AutomaticClientCertificate.cs:54`

Location: `Curl.Networking.UnitLibrary/AutomaticClientCertificate.cs:54`

Step 3, Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0: '&& now.UtcDateTime <= certificate.NotAfter.ToUniversalTime()' mutated to '<' survived. Qualifies decides which store certificate --ssl-auto-client-cert presents. The doc comment says the time must lie within the certificate's validity, inclusive. AutomaticClientCertificateTests only tests times days away from NotAfter (Now.AddDays(daysFromNow)), so nothing pins the inclusive end. At its last valid instant a certificate would stop being offered and the handshake would go out without a client certificate.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/AutomaticClientCertificate.cs:54:<= -Member Qualifies -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-13: Backlog -> Doing.
