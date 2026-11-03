---
id: AF-0572
title: Lib3103And3104_AskTheProxyForTheSiteWithTheirSharedCookie checks the share only for null
auditor: quality
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitTests/Lib3101To5004Tests.cs:Lib3103And3104_AskTheProxyForTheSiteWithTheirSharedCookie:weak-assertion
reproduction: none
task: BL-4736
tasks: BL-4736
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0572 - Lib3103And3104_AskTheProxyForTheSiteWithTheirSharedCookie checks the share only for null

## Summary

Low finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib3101To5004Tests.cs:175`: Lib3103And3104_AskTheProxyForTheSiteWithTheirSharedCookie checks the share only for null. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib3101To5004Tests.cs:175`

Step 0 weak-assertion. The name promises a shared cookie; the only check of engine.LastShare is Assert.IsNotNull(engine.LastShare);, never that it shares CurlLockData.Cookie (the pattern AF-0549 fixed for Lib1678), so a share sharing nothing passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib3101To5004Tests.cs -Pattern 'Assert.IsNotNull\(engine.LastShare\)'
```

- Expected: No match: the test asserts engine.LastShare.Shared holds CurlLockData.Cookie.
- Actual: Lib3101To5004Tests.cs: Assert.IsNotNull(engine.LastShare);

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
