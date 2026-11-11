---
id: BL-4887
title: Fix AF-0693: Surviving mutant: curl_url_set can accept a host starting with a delimiter such as '@'
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/UrlPartSetter.cs, Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.Set.cs]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-4887 — Fix AF-0693: Surviving mutant: curl_url_set can accept a host starting with a delimiter such as '@'

## Goal

The defect the audit office reported as AF-0693 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0693 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0693-surviving-mutant-curl-url-set-can-accept-a-host-st.md`.

Location: `Curl.Engine.UnitLibrary/UrlPartSetter.cs:152`

Location: `Curl.Engine.UnitLibrary/UrlPartSetter.cs:152`

': host.AsSpan().IndexOfAny(HostDelimiters) >= 0;' mutated to '> 0' survived. HostDelimiters is "/?#@:". With the mutant, a delimiter at index 0 (e.g. CURLUPART_HOST "@example.com") is no longer refused with CURLUE_BAD_HOSTNAME, and 'http://@example.com/' then parses. No test sets a host whose first character is a delimiter.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/UrlPartSetter.cs:152:>= -Member CheckedHost -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Touches narrowed from the whole Curl.Engine.UnitLibrary to the mutated file, and Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.Set.cs added, where the killing test belongs (the fix is a test; no other task in Doing names it).
- Cause: no test set a host whose first character is one of HostDelimiters, so `>= 0` -> `> 0` in `CheckedHost` changed nothing tested. Added `[DataRow("@example.com")]` to `Set_BadHost_ReturnsBadHostnameAndKeepsTheHost`: CURLUPART_HOST "@example.com" is refused with CURLUE_BAD_HOSTNAME, as upstream's hostname_check refuses `@`.
- Reproduction: a lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied by hand (`> 0` at UrlPartSetter.cs:152): the new row failed (1 of 9) and the mutant was reverted; unmutated it passes. No production code changed, so no quality run was needed.
- Full build clean (0 warnings); fast tests green (115 test projects, none failed). The test is platform-neutral (in-memory URL handle).

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-06: Doing -> Done. A CURLUPART_HOST starting with a delimiter such as '@' is pinned as BadHostname, killing the AF-0693 mutant
