---
id: BL-4699
title: Fix AF-0551: ConnectsToTheAddressTheServerAnswered checks only that a connection exists
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.IntegrationTests]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4699 — Fix AF-0551: ConnectsToTheAddressTheServerAnswered checks only that a connection exists

## Goal

The defect the audit office reported as AF-0551 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0551 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0551-connectstotheaddresstheserveranswered-checks-only.md`.

Location: `Curl.Engine.IntegrationTests/EngineCompositionDnsServersIntegrationTests.cs:45`

Location: `Curl.Engine.IntegrationTests/EngineCompositionDnsServersIntegrationTests.cs:45`

'Assert.IsNotNull(result.Connection, result.ErrorMessage);' is the test's only assertion; the dialled address and whether the DNS server was asked are never compared.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.IntegrationTests/EngineCompositionDnsServersIntegrationTests.cs -Pattern 'Assert\.'
```

- Expected: Assertions on the dialled end point and the query the server received.
- Actual: One line: Assert.IsNotNull(result.Connection, result.ErrorMessage);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

The test now returns the queried name from the fake DNS server and asserts it, plus the dialled address and port. Integration test passes; only a test file changed, so the fast tests are unaffected (build clean).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. ConnectsToTheAddressTheServerAnswered now asserts queried name and dialled endpoint
