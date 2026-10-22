---
id: BL-2046
title: Fix AF-0153: KDC proxy reply limit `reply.Length > MaximumReplyLength` can become `>=` with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-11
completed: 2026-10-11
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `PostAsync_ReplyExactlyAtTheLimit_ReturnsTheBody` to `Curl.Networking.UnitTests/KerberosKdcProxyHttpsTransportTests.cs`: a 200 reply of exactly `MaximumReplyLength` bytes returns its body, so the `>=` mutant now fails it. No production change: the `>` is right (MIT accepts a reply at the limit).
- `touches` widened to `Curl.Networking.UnitTests`, where the test lives; no task in Doing on `origin/work/dark-factory` (BL-2043, BL-2050) names it.
- The finding's reproduction (`Audit/Tools/Invoke-MutationTest.ps1`) is refused to lanes by the audit guard, so the mutant was checked by hand: with line 133 changed to `>=`, the new test failed (1 failed, 19 passed); reverted, all 20 pass. The quality auditor's re-audit confirms it.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. A KDC proxy reply exactly at the 1 MiB limit is pinned by a test, so the >= mutant fails; build clean, fast tests green
