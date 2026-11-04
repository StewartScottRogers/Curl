---
id: BL-4832
title: Fix AF-0650: Host-mapping test names exit 3 but asserts only a false return
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4832 — Fix AF-0650: Host-mapping test names exit 3 but asserts only a false return

## Goal

The defect the audit office reported as AF-0650 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0650 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0650-host-mapping-test-names-exit-3-but-asserts-only-a.md`.

Location: `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs:568`

Location: `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs:568`

The name states curl refuses with exit 3. The only assertion is Assert.IsFalse(accepted). Exit 3 is never checked in this test or a helper it calls.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs -Pattern 'RefusesWithExit3_IsRefused' -Context 0,5
```

- Expected: An assertion of exit 3 (CurlExitCode.UrlMalformat), or a name without the number.
- Actual: Assert.IsFalse(accepted);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-11-04: CurlUrlHost.TryNormalizeReadingHost returns a CurlUrlRejection, not an exit code (the hosts map it to exit 3 later), so the test cannot assert exit 3 at this layer. Renamed it to TryNormalizeReadingHost_MeasuredArabicFormCurlRefuses_IsRefusedAsIdnConversionFailed and added an assertion that rejection is CurlUrlRejection.IdnConversionFailed (measured: all ten forms give it). The reproduction now finds no match. Test-only change in a test project nothing references: built and ran Curl.Protocol.Abstractions.UnitTests fast tests, 1636 passed, 2 skipped, 0 failed. No production code changed, so no quality run.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. Host-mapping refusal test is named for and asserts the IdnConversionFailed rejection rather than claiming exit 3
