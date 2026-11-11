---
id: BL-4895
title: Fix AF-0701: Surviving mutant: a final zero-length SvcParam (e.g. no-default-alpn) can drop from the DoH HTTPS-record trace
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests/DohDnsResolverHttpsRecordTraceTests.cs]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-4895 — Fix AF-0701: Surviving mutant: a final zero-length SvcParam (e.g. no-default-alpn) can drop from the DoH HTTPS-record trace

## Goal

The defect the audit office reported as AF-0701 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0701 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0701-surviving-mutant-a-final-zero-length-svcparam-e-g.md`.

Location: `Curl.Networking.UnitLibrary/DohTraceLines.HttpsRecord.cs:140`

Location: `Curl.Networking.UnitLibrary/DohTraceLines.HttpsRecord.cs:140`

'while (length == 0 && record.Length - position >= 4)' mutated to '> 4' survived. A last SvcParam with an empty value (key 2, no-default-alpn) leaves exactly 4 bytes. With the mutant its end is never yielded, so the verbose DoH trace line for the record changes. No test ends a record with a zero-length parameter.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DohTraceLines.HttpsRecord.cs:140:>= -Member FirstHttpsRecordLines -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `ResolveHttpsRecordAsync_ARecordEndingWithAnEmptyNoDefaultAlpn_WritesTheRecordAfterIt` to `Curl.Networking.UnitTests/DohDnsResolverHttpsRecordTraceTests.cs`: a record (alpn h2, then an empty no-default-alpn as its last parameter, leaving exactly 4 bytes) must write the line after each parameter. No production change.
- Widened `touches` with that test file (the task named only the library; no other task in Doing names it).
- The reproduction script lives under `Audit/`, which a lane may not run, so the mutant was applied by hand (`>= 4` to `> 4` at `DohTraceLines.HttpsRecord.cs:140`): the new test failed (killed), then the line was restored. The re-audit confirms with the script itself.
- `dotnet build` clean; fast tests green in every project (Curl.Networking.UnitTests: 4034 passed, 33 skipped). The test is pure bytes, so it is platform-neutral. No Measure-CodeQuality run, since no production code changed.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-06: Doing -> Done. A test ending an HTTPS record with an empty no-default-alpn kills AF-0701's mutant
