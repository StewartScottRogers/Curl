---
id: BL-5225
title: Make the Oci None reporter test assert that nothing was reported (AF-0759 fix falls short)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.OciRegistry.UnitTests/Http/OciHttpHeadReporterTests.cs]
requirement: none
created: 2026-11-15
completed: 2026-11-15
---
# BL-5225 — Make the Oci None reporter test assert that nothing was reported (AF-0759 fix falls short)

## Goal

`OciHttpHeadReporterTests.ReportResponseLineAsync_None_ShowsNothing` asserts that the `None` reporter recorded nothing, as audit finding AF-0759 expects. Today it checks only that a task completed.

## Context

The shift-end review of 2026-11-15 (ADR-0710) found this with the silent-failure-hunter and code-reviewer agents. It reviewed the shift merged at d04b275ae7.

- BL-5208 replaced `Assert.IsNotNull(OciHttpHeadReporter.None)` in `Curl.Extended.Protocol.OciRegistry.UnitTests/Http/OciHttpHeadReporterTests.cs:106-112`. The new check is `Assert.IsTrue(response.IsCompletedSuccessfully)`.
- AF-0759 expects "an assertion that nothing was reported". A reporter that kept the line, or wrote it, would still pass the new check, so the quality auditor's re-audit will see the finding still reproducing.
- The sibling fixes do assert on recorded state. The S3 test (BL-5210) asserts `Assert.IsNull(none.Report)`. The Azure test asserts `Report` is null after a non-status line.
- `OciHttpHeadReporter` exposes `TransferReport? Report` (`Curl.Extended.Protocol.OciRegistry.UnitLibrary/Http/OciHttpHeadReporter.cs:31`). The fix can use the same assertion as those tests, and can keep the test `async` and await the call as they do.

## Acceptance criteria

- [x] `ReportResponseLineAsync_None_ShowsNothing` in `OciHttpHeadReporterTests.cs` asserts on the reporter's recorded state, for example `Assert.IsNull(none.Report)` after a non-status line. It does not rely only on `IsCompletedSuccessfully`.
- [x] The test holds one `OciHttpHeadReporter.None` instance in a local and calls both report methods on it, since `None` is a property that returns a new instance each time.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Test now holds one `None` instance, awaits both report calls and asserts `Assert.IsNull(none.Report)`. Built; OciRegistry.UnitTests 189 passed (only a test file changed, so no library quality run).

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: 91151eb4 a7fa7596 9742157d.
- 2026-11-15: Doing -> Done. None reporter test asserts Report is null on one shared instance
