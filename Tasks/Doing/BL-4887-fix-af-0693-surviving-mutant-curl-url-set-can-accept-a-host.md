---
id: BL-4887
title: Fix AF-0693: Surviving mutant: curl_url_set can accept a host starting with a delimiter such as '@'
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-06
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
