---
id: BL-4712
title: Fix AF-0564: Surviving mutant: a lib1560 scheme-guess table row can change without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4695]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests/Lib1560TablesTests.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4712 — Fix AF-0564: Surviving mutant: a lib1560 scheme-guess table row can change without a test failing

## Goal

The defect the audit office reported as AF-0564 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0564 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0564-surviving-mutant-a-lib1560-scheme-guess-table-row.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.Handles.cs:22`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.Handles.cs:22`

Mutant new("smtp.example.com:99/moo", false, ...) -> new("smtp.example.com:99/moo", true, ...) survived. The row is upstream lib1560's case and Lib1560.RunAsync checks every row (checks.SchemeGuess(Lib1560Tables.SchemeGuesses)), but no unit test pins the rows, so a wrong row changes what the port checks and what it prints unnoticed.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.Handles.cs:22:false -Member SchemeGuesses -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-11-03 (lane 5, first claim, no code edited): the mutant cannot be killed in the library. `Lib1560Checks.SchemeGuess` matches upstream lib1560's `test_scheme_guess`, which flags a row only when the set fails and the row does not expect it (`if(rc && !guess[i].fails)`); a row that expects failure but parses is not reported upstream either. So flipping `ExpectedFail` false -> true on a row that parses is invisible to the check, and making the check stricter would stop matching upstream. Decision: kill it with a unit test that pins every `Lib1560Tables.SchemeGuesses` row (URL, ExpectedFail, SetFlags, Scheme, GetFlags) against upstream's `guess[]` table, in a new file `Curl.Conformance.LibTest.UnitTests/Lib1560TablesTests.cs` (added to `touches`). No production change is needed.
- Waits on BL-4695: its `touches` names the whole `Curl.Conformance.LibTest.UnitTests` project, which this test has to go in, so by rule 3 it is in `depends-on` and the task is back in Backlog. Next claim: write that one test file (one `[TestMethod]` asserting the 18 rows in order, compared with `CollectionAssert.AreEqual` on the records), then run the reproduction (`Invoke-MutationTest.ps1 ... Lib1560Tables.Handles.cs:22:false -Member SchemeGuesses`) to confirm it is killed.

- 2026-11-03 (lane 3, second claim): added `Curl.Conformance.LibTest.UnitTests/Lib1560TablesTests.cs`, one test pinning all 18 `SchemeGuesses` rows against upstream's `guess[]` with `CollectionAssert.AreEqual`. The audit guard refuses a lane running `Audit/Tools/Invoke-MutationTest.ps1`, so the reproduction was applied by hand: row 22 flipped false -> true with the Edit tool, the test failed (killed), the row was reverted. `dotnet build` is clean and the fast tests are green. Platform-neutral: no paths, no OS text.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Backlog. Waits on BL-4695, whose touches hold Curl.Conformance.LibTest.UnitTests, where the table-pinning test Lib1560TablesTests.cs must go
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. Lib1560TablesTests pins every lib1560 scheme-guess row, killing the AF-0564 mutant
