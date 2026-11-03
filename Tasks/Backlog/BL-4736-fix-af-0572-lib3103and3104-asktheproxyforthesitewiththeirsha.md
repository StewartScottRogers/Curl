---
id: BL-4736
title: Fix AF-0572: Lib3103And3104_AskTheProxyForTheSiteWithTheirSharedCookie checks the share only for null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4736 — Fix AF-0572: Lib3103And3104_AskTheProxyForTheSiteWithTheirSharedCookie checks the share only for null

## Goal

The defect the audit office reported as AF-0572 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0572 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0572-lib3103and3104-asktheproxyforthesitewiththeirshare.md`.

Location: `Curl.Conformance.LibTest.UnitTests/Lib3101To5004Tests.cs:175`

Location: `Curl.Conformance.LibTest.UnitTests/Lib3101To5004Tests.cs:175`

Step 0 weak-assertion. The name promises a shared cookie; the only check of engine.LastShare is Assert.IsNotNull(engine.LastShare);, never that it shares CurlLockData.Cookie (the pattern AF-0549 fixed for Lib1678), so a share sharing nothing passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib3101To5004Tests.cs -Pattern 'Assert.IsNotNull\(engine.LastShare\)'
```

- Expected: No match: the test asserts engine.LastShare.Shared holds CurlLockData.Cookie.
- Actual: Lib3101To5004Tests.cs: Assert.IsNotNull(engine.LastShare);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
