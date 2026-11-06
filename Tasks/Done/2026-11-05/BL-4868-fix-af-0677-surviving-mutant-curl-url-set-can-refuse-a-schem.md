---
id: BL-4868
title: Fix AF-0677: Surviving mutant: curl_url_set can refuse a scheme of exactly 40 characters as a bad scheme
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4866]
touches: [Curl.Engine.UnitLibrary/UrlPartSetter.cs, Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.Set.cs]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-4868 — Fix AF-0677: Surviving mutant: curl_url_set can refuse a scheme of exactly 40 characters as a bad scheme

## Goal

The defect the audit office reported as AF-0677 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0677 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0677-surviving-mutant-curl-url-set-can-refuse-a-scheme.md`.

Location: `Curl.Engine.UnitLibrary/UrlPartSetter.cs:113`

Location: `Curl.Engine.UnitLibrary/UrlPartSetter.cs:113`

Mutant 'value.Length is > 0 and <= MaximumSchemeLength' -> 'value.Length is > 0 and < MaximumSchemeLength' (MaximumSchemeLength = 40, UrlPartSetter.cs:19) survived in the seed-843793 sample, and survived again in the targeted -Site run. SchemeCode returns CurlUrlCode.BadScheme when HasSchemeLength is false, so a 40-character scheme, which curl accepts, would get CURLUE_BAD_SCHEME back from the URL API. The libcurl return code changes and no test pins the boundary.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/UrlPartSetter.cs:113:<= -Member HasSchemeLength -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant is killed.
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-11-05 lane 3: The production code is already right (upstream `curl_url_set` refuses a scheme only when `plen > MAX_SCHEME_LEN` or `plen < 1`), so the fix is a test only: in `Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.Set.cs`, pin a 40-character scheme set under `CurlUrlFlags.NonSupportScheme` as `CurlUrlCode.Ok` (the 41-character one is already pinned as `BadScheme` in `Set_BadScheme_ReturnsUpstreamsCodeAndKeepsTheScheme`). `touches` narrowed from the whole `Curl.Engine.UnitLibrary` to `UrlPartSetter.cs`, and widened to that test file because the fix lands there. BL-4866 (Doing) holds the whole `Curl.Engine.UnitTests`, so this task waits on it. No code edits made.
- 2026-11-05 lane 1: Added `Set_SchemeOfExactlyTheMaximumLength_IsAccepted` to `ManagedCurlUrlHandleTests.Set.cs`: a 40-character scheme set under `CurlUrlFlags.NonSupportScheme` answers `CurlUrlCode.Ok` and is read back. Test only; `UrlPartSetter.cs` is unchanged, so no quality run was needed. A lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the reproduction was checked by hand: with line 113 changed to `< MaximumSchemeLength`, the new test failed (1 of 1), so the mutant is killed; the line was then put back. The re-audit runs the tool itself. `dotnet build` clean and the fast tests green (115 test projects, 0 failed); the test is platform-neutral. One Curl.Engine.UnitTests test failed once in the first run and passed on the rerun; the output did not name it, so no flaky-test task was filed.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-05: Doing -> Backlog. Waits on BL-4866, which holds the whole Curl.Engine.UnitTests where the 40-character scheme test lands
- 2026-11-05: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-05: Doing -> Done. A 40-character scheme is pinned as accepted by curl_url_set, killing AF-0677's mutant
