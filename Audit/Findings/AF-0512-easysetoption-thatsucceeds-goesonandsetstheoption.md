---
id: AF-0512
title: EasySetOption_ThatSucceeds_GoesOnAndSetsTheOption never checks that the option was set
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitTests/LibTestStepsTests.cs:EasySetOption_ThatSucceeds_GoesOnAndSetsTheOption:name-lies
reproduction: none
task: none
tasks:
found: 2026-10-31
found-at: 23bacbc6c04b8e06a4eecb79636da4891dca4c01
scorecard: 2026-10-31_1149.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0512 - EasySetOption_ThatSucceeds_GoesOnAndSetsTheOption never checks that the option was set

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/LibTestStepsTests.cs:104`: EasySetOption_ThatSucceeds_GoesOnAndSetsTheOption never checks that the option was set.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/LibTestStepsTests.cs:104`

The body calls steps.EasySetOption(easy, CurlOption.Url, "http://127.0.0.1/500") and asserts only Assert.IsTrue(goesOn), Assert.AreEqual(CurlCode.Ok, steps.Result) and Assert.AreEqual(string.Empty, error.ToString()). Nothing reads the handle's (or the FakeCurlEngine's) URL. An EasySetOption that answered Ok without passing the option on to curl_easy_setopt would pass, yet the name promises 'SetsTheOption'.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/LibTestStepsTests.cs -Pattern 'EasySetOption_ThatSucceeds_GoesOnAndSetsTheOption' -Context 0,11
```

- Expected: The body asserts the handle's URL is http://127.0.0.1/500
- Actual: Its three assertions are goesOn, steps.Result == Ok and an empty error writer; none reads the option back

## Re-audits

## Log

- 2026-10-31: filed proposed.
