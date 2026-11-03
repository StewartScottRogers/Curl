---
id: AF-0590
title: Init_WithoutAnEngine_..._AndCreatesEasyTransfersFromIt checks the transfer only for null
auditor: quality
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Libcurl.UnitTests/Global/CurlGlobalTests.cs:Init_WithoutAnEngine_BuildsTheManagedEngine_AndCreatesEasyTransfersFromIt:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0590 - Init_WithoutAnEngine_..._AndCreatesEasyTransfersFromIt checks the transfer only for null

## Summary

Low finding from the quality auditor at `Libcurl.UnitTests/Global/CurlGlobalTests.cs:75`: Init_WithoutAnEngine_..._AndCreatesEasyTransfersFromIt checks the transfer only for null. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Libcurl.UnitTests/Global/CurlGlobalTests.cs:75`

Step 0 weak-assertion. The name says the easy transfers come from the managed engine; Assert.IsNotNull(transfer); is the only check of transfer, so one created elsewhere would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Libcurl.UnitTests/Global/CurlGlobalTests.cs -Pattern 'Assert.IsNotNull\(transfer\)'
```

- Expected: No match: the test asserts the transfer is the managed engine's (e.g. its type).
- Actual: CurlGlobalTests.cs:75: Assert.IsNotNull(transfer);

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
