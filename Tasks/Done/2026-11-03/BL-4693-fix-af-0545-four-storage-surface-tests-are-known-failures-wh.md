---
id: BL-4693
title: Fix AF-0545: Four storage surface tests are known failures whose catch (Exception) lets them pass on any failure while their named behaviour is broken
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitLibrary, Curl.Conformance.Extended.UnitTests/Harness/KnownFailuresTests.cs, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4693 — Fix AF-0545: Four storage surface tests are known failures whose catch (Exception) lets them pass on any failure while their named behaviour is broken

## Goal

The defect the audit office reported as AF-0545 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0545 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0545-four-storage-surface-tests-are-known-failures-whos.md`.

Location: `Curl.Conformance.Extended.UnitLibrary/Harness/KnownFailures.cs:108`

Location: `Curl.Conformance.Extended.UnitLibrary/Harness/KnownFailures.cs:108`

KnownFailures.txt lists CurlSurface:FailWithBody-Gs-1, FailWithBody-S3-1, Verbose-Oci-1 and FailWithBody-Oci-1. For a listed requirement RunCaseAsync does 'try { await runCase().ConfigureAwait(false); } catch (Exception) { return; }', so GsTransferCases/S3TransferCases/OciTransferCases.FailWithBody_MissingObject_Exits22AndWritesTheErrorBody and OciTransferCases.Verbose_Download_TellsTheConnectionAndTlsOnStandardErrorOnly pass whatever goes wrong: any exception, a crash included, not only the known difference. They work like [Ignore]d tests whose names promise exit 22 and the error body.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.Extended.UnitLibrary/Harness/KnownFailures.cs -Pattern 'catch \(Exception\)' -Context 0,2; Select-String -Path Curl.Conformance.Extended.UnitTests/KnownFailures.txt -Pattern '^CurlSurface'
```

- Expected: No known-failure entries, or a catch limited to the recorded difference.
- Actual: KnownFailures.cs:108: catch (Exception) { return; } and four CurlSurface lines in KnownFailures.txt.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The four CurlSurface lines were already gone from KnownFailures.txt when this run started (their storage fixes landed first), so the remaining half of the finding was the catch.
- Decision (sensible default): `KnownFailures.RunCaseAsync` now swallows only a failed assertion, an exception whose type is or derives from MSTest's `UnitTestAssertException`, matched by full type name so the library takes no test-framework reference. Anything else, a crash included, fails a listed case. No ADR, since this is a harness test rule and not curl behaviour.
- Touches widened to `Curl.Conformance.Extended.UnitTests/Harness/KnownFailuresTests.cs` (no task in Doing on origin/work/dark-factory names it) for the tests of the narrowed catch.
- Also removed the KnownFailures.txt lines naming BL-4721 (ProtoJson-Wrappers-1/2, Struct-1/2, FieldMask-1; BL-4721 is Done): those cases pass since 63512cbdd, so they fail with KnownFailurePassedException on origin/work/dark-factory too, independent of this change, and the quality run will not measure while they fail. BL-4720 (Doing) also touches that file; this is a CI fix to a red test, so per the CI-fix rule it is changed here and BL-4720 rebases over it.
- Verified: the reproduction prints nothing (no `catch (Exception)`, no CurlSurface lines); Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary reports 0 failing members; dotnet build 0 warnings; the fast tests pass in all 115 test projects. Platform-neutral: no paths, OS errors or certificates involved.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. A listed known failure now passes only on a failed assertion; a crash fails it
