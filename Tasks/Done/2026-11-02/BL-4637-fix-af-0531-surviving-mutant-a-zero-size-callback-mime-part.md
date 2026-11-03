---
id: BL-4637
title: Fix AF-0531: Surviving mutant: a zero-size callback MIME part can lose its known length without a test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-03
completed: 2026-11-02
---
# BL-4637 — Fix AF-0531: Surviving mutant: a zero-size callback MIME part can lose its known length without a test failing

## Goal

The defect the audit office reported as AF-0531 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0531 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0531-surviving-mutant-a-zero-size-callback-mime-part-ca.md`.

Location: `Curl.Engine.UnitLibrary/MultipartFormPartMapping.cs:116`

Location: `Curl.Engine.UnitLibrary/MultipartFormPartMapping.cs:116`

Mutant 'data is EasyMimeCallback { Size: >= 0 } sized ? sized.Size : null' -> '{ Size: > 0 }' survived (Curl.Engine.UnitTests green). A callback part whose CURLOPT/curl_mime_data_cb size is 0 then maps to an unknown length (null), the same as -1. The multipart body's length becomes unknown, and the request is framed differently (chunked or without Content-Length) where curl sends a known Content-Length. No test drives a zero-size callback part, so request bytes can change unnoticed.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/MultipartFormPartMapping.cs:116:>= -Member ContentLengthOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The fix is test-only: `MultipartFormPartMappingTests.FromMimeBody_CallbackData_IsAReaderPartOverTheCallbackKeptInCallbackReads` gains `[DataRow(0L, 0L)]`, so a zero-size `curl_mime_data_cb` part must keep its known length 0. No production code changed, so no Measure-CodeQuality run was needed.
- A lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the reproduction was checked by hand: with `ContentLengthOf`'s pattern changed to `{ Size: > 0 }` in the working tree, the test failed (expected 0, got null), so the mutant is killed; then the pattern went back to `>= 0`. The re-audit runs the tool itself.
- `dotnet build` was clean, and the fast tests passed in all 115 test projects on Windows. The change is a platform-neutral DataRow.

## Log

- 2026-11-03: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-02: Doing -> Done. A zero-size callback MIME part's known length 0 is pinned by a test, killing AF-0531's mutant
