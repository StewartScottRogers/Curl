---
id: BL-4833
title: Fix AF-0651: Host-mapping test names exit 27 but asserts only acceptance and the converted text
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-4833 — Fix AF-0651: Host-mapping test names exit 27 but asserts only acceptance and the converted text

## Goal

The defect the audit office reported as AF-0651 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0651 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0651-host-mapping-test-names-exit-27-but-asserts-only-a.md`.

Location: `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs:580`

Location: `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs:580`

The name states exit 27 through a proxy. Its helper AssertConvertsTo asserts only Assert.IsTrue(accepted) and Assert.AreEqual(expected, idnHost). Exit 27 is never checked.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs -Pattern 'private static void AssertConvertsTo' -Context 0,6
```

- Expected: An assertion of exit 27, or a name without the number.
- Actual: Assert.IsTrue(accepted); Assert.AreEqual(expected, idnHost);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
