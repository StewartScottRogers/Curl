---
id: AF-0545
title: Four storage surface tests are known failures whose catch (Exception) lets them pass on any failure while their named behaviour is broken
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.Extended.UnitLibrary/Harness/KnownFailures.cs:RunCaseAsync:ignored-test
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0545 - Four storage surface tests are known failures whose catch (Exception) lets them pass on any failure while their named behaviour is broken

## Summary

Medium finding from the quality auditor at `Curl.Conformance.Extended.UnitLibrary/Harness/KnownFailures.cs:108`: Four storage surface tests are known failures whose catch (Exception) lets them pass on any failure while their named behaviour is broken. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Conformance.Extended.UnitLibrary/Harness/KnownFailures.cs:108`

KnownFailures.txt lists CurlSurface:FailWithBody-Gs-1, FailWithBody-S3-1, Verbose-Oci-1 and FailWithBody-Oci-1. For a listed requirement RunCaseAsync does 'try { await runCase().ConfigureAwait(false); } catch (Exception) { return; }', so GsTransferCases/S3TransferCases/OciTransferCases.FailWithBody_MissingObject_Exits22AndWritesTheErrorBody and OciTransferCases.Verbose_Download_TellsTheConnectionAndTlsOnStandardErrorOnly pass whatever goes wrong: any exception, a crash included, not only the known difference. They work like [Ignore]d tests whose names promise exit 22 and the error body.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.Extended.UnitLibrary/Harness/KnownFailures.cs -Pattern 'catch \(Exception\)' -Context 0,2; Select-String -Path Curl.Conformance.Extended.UnitTests/KnownFailures.txt -Pattern '^CurlSurface'
```

- Expected: No known-failure entries, or a catch limited to the recorded difference.
- Actual: KnownFailures.cs:108: catch (Exception) { return; } and four CurlSurface lines in KnownFailures.txt.

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
