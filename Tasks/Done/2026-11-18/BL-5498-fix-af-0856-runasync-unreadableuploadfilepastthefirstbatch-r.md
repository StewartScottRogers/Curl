---
id: BL-5498
title: Fix AF-0856: RunAsync_UnreadableUploadFilePastTheFirstBatch_ReportsExit26WhateverFailedBeforeAndStartsNothingMore asserts exit 25, not 26
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr170Tests.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5498 — Fix AF-0856: RunAsync_UnreadableUploadFilePastTheFirstBatch_ReportsExit26WhateverFailedBeforeAndStartsNothingMore asserts exit 25, not 26

## Goal

The defect the audit office reported as AF-0856 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0856 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Console.UnitTests/Cgr170Tests.cs:89`

Location: `Curl.Console.UnitTests/Cgr170Tests.cs:89`

The name promises exit 26. The body asserts line 98 Assert.AreEqual(25, exitCode); and line 99 stdout "0 7\n1 7\n2 25\n". Nothing checks 26 or CurlExitCode.ReadError.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/Cgr170Tests.cs -Pattern 'ReportsExit26WhateverFailedBefore|Assert.AreEqual\(25, exitCode\)'
```

- Expected: The name and the asserted exit code agree.
- Actual: Cgr170Tests.cs:89 ...ReportsExit26WhateverFailedBeforeAndStartsNothingMore() and Cgr170Tests.cs:98 Assert.AreEqual(25, exitCode);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

The test already asserted exit 26 and stdout "0 7
1 7
2 26
" in this tree (fixed by an earlier change to Cgr170Tests.cs); the reproduction finds no `Assert.AreEqual(25, exitCode)`. Cgr170Tests: 7 passed. No code change needed.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-18: Doing -> Done. Test already asserts exit 26 matching its name; reproduction clean, Cgr170Tests pass
