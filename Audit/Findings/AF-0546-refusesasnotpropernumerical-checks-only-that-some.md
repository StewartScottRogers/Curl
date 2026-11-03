---
id: AF-0546
title: RefusesAsNotProperNumerical checks only that some refusal exists, not its text
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Cli.UnitTests/CommandLineNumberTests.cs:ParseMinusOneOrMore_TwoToThe31UnderWindowsCeiling_RefusesAsNotProperNumerical:weak-assertion
reproduction: none
task: BL-4694
tasks: BL-4694
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0546 - RefusesAsNotProperNumerical checks only that some refusal exists, not its text

## Summary

Medium finding from the quality auditor at `Curl.Cli.UnitTests/CommandLineNumberTests.cs:175`: RefusesAsNotProperNumerical checks only that some refusal exists, not its text. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Cli.UnitTests/CommandLineNumberTests.cs:175`

The only check of the refusal is 'Assert.IsNotNull(refusal);'; the other assertions check number == 0. The name promises the 'not proper numerical' refusal, yet any refusal message or exit code passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Cli.UnitTests/CommandLineNumberTests.cs -Pattern 'RefusesAsNotProperNumerical' -Context 0,7
```

- Expected: The test asserts the refusal's standard-error line and exit code.
- Actual: Only Assert.IsNotNull(refusal); then Assert.AreEqual(0L, number);

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
