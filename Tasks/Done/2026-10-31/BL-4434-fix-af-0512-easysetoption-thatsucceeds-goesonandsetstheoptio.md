---
id: BL-4434
title: Fix AF-0512: EasySetOption_ThatSucceeds_GoesOnAndSetsTheOption never checks that the option was set
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-31
completed: 2026-10-31
---
# BL-4434 — Fix AF-0512: EasySetOption_ThatSucceeds_GoesOnAndSetsTheOption never checks that the option was set

## Goal

The defect the audit office reported as AF-0512 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0512 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0512-easysetoption-thatsucceeds-goesonandsetstheoption.md`.

Location: `Curl.Conformance.LibTest.UnitTests/LibTestStepsTests.cs:104`

Location: `Curl.Conformance.LibTest.UnitTests/LibTestStepsTests.cs:104`

The body calls steps.EasySetOption(easy, CurlOption.Url, "http://127.0.0.1/500") and asserts only Assert.IsTrue(goesOn), Assert.AreEqual(CurlCode.Ok, steps.Result) and Assert.AreEqual(string.Empty, error.ToString()). Nothing reads the handle's (or the FakeCurlEngine's) URL. An EasySetOption that answered Ok without passing the option on to curl_easy_setopt would pass, yet the name promises 'SetsTheOption'.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/LibTestStepsTests.cs -Pattern 'EasySetOption_ThatSucceeds_GoesOnAndSetsTheOption' -Context 0,11
```

- Expected: The body asserts the handle's URL is http://127.0.0.1/500
- Actual: Its three assertions are goesOn, steps.Result == Ok and an empty error writer; none reads the option back

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

The test now performs the transfer through FakeCurlEngine.Performing and asserts Options.Network.Url is http://127.0.0.1/500.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-31: Doing -> Done. Test now asserts the URL reached the engine
