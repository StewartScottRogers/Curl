---
id: AF-0512
title: EasySetOption_ThatSucceeds_GoesOnAndSetsTheOption never checks that the option was set
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-01_0144.md: the reproduction no longer reproduces.
key: quality:Curl.Conformance.LibTest.UnitTests/LibTestStepsTests.cs:EasySetOption_ThatSucceeds_GoesOnAndSetsTheOption:name-lies
reproduction: none
task: BL-4434
tasks: BL-4434
found: 2026-10-31
found-at: 23bacbc6c04b8e06a4eecb79636da4891dca4c01
scorecard: 2026-10-31_1149.md
duplicate-of:
closed: 2026-11-01
closed-how: reliable-reaudit
closed-by: 2026-11-01_0144.md
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

- 2026-11-01 | 2026-11-01_0144.md | reproduces: no | Ran the Select-String reproduction and read the whole test: EasySetOption_ThatSucceeds_GoesOnAndSetsTheOption (LibTestStepsTests.cs:104-119) has the fake engine capture o.Network.Url during PerformAsync, and line 116 asserts Assert.AreEqual("http://127.0.0.1/500", url), so the option being set is checked. The reproduction's -Context 0,11 window ends at line 115, one line before that assertion.

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> accepted.
- 2026-11-01: accepted -> closed. Re-audit 2026-11-01_0144.md: the reproduction no longer reproduces.
