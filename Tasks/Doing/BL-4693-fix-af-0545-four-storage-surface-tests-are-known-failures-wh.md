---
id: BL-4693
title: Fix AF-0545: Four storage surface tests are known failures whose catch (Exception) lets them pass on any failure while their named behaviour is broken
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
