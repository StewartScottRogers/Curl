---
id: BL-4886
title: Fix AF-0692: Surviving mutant: any write error (or any header callback that delivered 0) can drop the head from getinfo
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs, Curl.Engine.UnitTests/Cgr252Tests.cs]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-4886 — Fix AF-0692: Surviving mutant: any write error (or any header callback that delivered 0) can drop the head from getinfo

## Goal

The defect the audit office reported as AF-0692 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0692 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0692-surviving-mutant-any-write-error-or-any-header-cal.md`.

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:239`

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:239`

'result.ExitCode.ToCurlCode() == CurlCode.WriteError && lastHeaderCallbackOutput is { BytesDelivered: 0 }' mutated to '||' survived (Engine sample, seed 982898). HeaderCallbackTookNothing decides whether getinfo drops HeaderSize, ContentType and ContentLengthDownload (line 246, CGR-252). With '||', a body write error alone would zero the header size and content type that libcurl reports.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:239:&& -Member HeaderCallbackTookNothing -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Touches narrowed from the whole Curl.Engine.UnitLibrary to the mutated file and the CGR-252 test file, where the killing test belongs; Curl.Engine.UnitTests/Cgr252Tests.cs added because the fix is a test (no other task in Doing names it).
- Cause: no test performed a write error with the header callback having taken its lines (or unset), so `&&` -> `||` in `HeaderCallbackTookNothing` changed nothing tested. Added `PerformAsync_WriteCallbackRefusesTheBodyWithNoHeaderCallback_KeepsTheParsedHeaders` (Cgr252Tests.cs): the write callback returns 0, no header callback; getinfo keeps HeaderSize, ContentType and ContentLengthDownload. Commit 078401556.
- Reproduction: a lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied by hand (`||` at Finish.cs:239): the new test failed (HeaderSize 0) and was reverted; unmutated it passes. No production code changed, so no quality run was needed.
- Full build clean; fast tests green (114 test projects, no failure). The test is platform-neutral (in-memory scripted connector).

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-06: Doing -> Done. A body write error alone no longer can drop the head from getinfo untested: the AF-0692 mutant is killed by a CGR-252 test
