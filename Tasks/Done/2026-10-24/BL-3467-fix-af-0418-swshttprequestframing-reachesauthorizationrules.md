---
id: BL-3467
title: Fix AF-0418: SwsHttpRequestFraming.ReachesAuthorizationRules' ParseContentLength(...) < 0 can become <= 0 with no test failing, so a Content-Length: 0 request skips the stand-in's authorization rules
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-24
completed: 2026-10-24
---
# BL-3467 — Fix AF-0418: SwsHttpRequestFraming.ReachesAuthorizationRules' ParseContentLength(...) < 0 can become <= 0 with no test failing, so a Content-Length: 0 request skips the stand-in's authorization rules

## Goal

The defect the audit office reported as AF-0418 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0418 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0418-swshttprequestframing-reachesauthorizationrules-pa.md`.

Location: `Curl.Conformance.UnitLibrary/SwsHttpRequestFraming.cs:65`

Location: `Curl.Conformance.UnitLibrary/SwsHttpRequestFraming.cs:65`

Invoke-MutationTest.ps1 -Library Curl.Conformance.UnitLibrary -MaxMutants 40 -Seed 0: survived, '... && ParseContentLength(line[ContentLengthPrefix.Length..]) < 0))' -> '<= 0'. ParseContentLength returns -1 only for unparsable digits. With <= 0, a valid Content-Length: 0 makes ReachesAuthorizationRules return false, so the sws stand-in stops applying its auth rules to empty-body requests. That changes the replies upstream cases get, and no test pins it.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/SwsHttpRequestFraming.cs:65:< -Member ReachesAuthorizationRules -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Added a DataRow to SwsHttpServerConnectorTests.Authorization_SelectsThePartAsSwsDoes: Content-Length: 0 with Authorization: Digest must select data1000. The lane guard refuses the mutation script, so I applied the mutant (< 0 to <= 0) by hand: the new row fails (killed), then restored. Build clean, 4373 fast tests pass.

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-24: Doing -> Done. Content-Length: 0 request now pinned to reach sws auth rules; the <= 0 mutant is killed
