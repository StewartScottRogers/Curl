---
id: BL-2046
title: Fix AF-0153: KDC proxy reply limit `reply.Length > MaximumReplyLength` can become `>=` with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-11
completed:
---
# BL-2046 — Fix AF-0153: KDC proxy reply limit `reply.Length > MaximumReplyLength` can become `>=` with no test failing

## Goal

The defect the audit office reported as AF-0153 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0153 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0153-kdc-proxy-reply-limit-reply-length-maximumreplylen.md`.

Location: `Curl.Networking.UnitLibrary/KerberosKdcProxyHttpsTransport.cs:133`

Location: `Curl.Networking.UnitLibrary/KerberosKdcProxyHttpsTransport.cs:133`

Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -ExcludeBaselineFailures: `if (reply.Length > MaximumReplyLength)` mutated to `if (reply.Length >= MaximumReplyLength)` survived. MaximumReplyLength is 1024 * 1024. The mutant refuses a KDC proxy reply of exactly 1 MiB with an IOException, so Kerberos through the KDC proxy fails where it should succeed. No test sends a reply at the limit. This is a refusal decision, an off-by-one at a limit no test pins.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/KerberosKdcProxyHttpsTransport.cs:133:> -Member ReadToEndAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-11: Created.
