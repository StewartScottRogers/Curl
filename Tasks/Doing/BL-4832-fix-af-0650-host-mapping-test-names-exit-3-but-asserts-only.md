---
id: BL-4832
title: Fix AF-0650: Host-mapping test names exit 3 but asserts only a false return
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-4832 — Fix AF-0650: Host-mapping test names exit 3 but asserts only a false return

## Goal

The defect the audit office reported as AF-0650 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0650 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0650-host-mapping-test-names-exit-3-but-asserts-only-a.md`.

Location: `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs:568`

Location: `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs:568`

The name states curl refuses with exit 3. The only assertion is Assert.IsFalse(accepted). Exit 3 is never checked in this test or a helper it calls.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs -Pattern 'RefusesWithExit3_IsRefused' -Context 0,5
```

- Expected: An assertion of exit 3 (CurlExitCode.UrlMalformat), or a name without the number.
- Actual: Assert.IsFalse(accepted);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
