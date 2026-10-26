---
id: BL-3526
title: Fix AF-0449: CreateKdcClient_Configuration_MakesAClient's only assertion is IsNotNull on a constructed client
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests]
requirement: none
created: 2026-10-25
completed:
---
# BL-3526 — Fix AF-0449: CreateKdcClient_Configuration_MakesAClient's only assertion is IsNotNull on a constructed client

## Goal

The defect the audit office reported as AF-0449 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0449 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0449-createkdcclient-configuration-makesaclient-s-only.md`.

Location: `Curl.Engine.UnitTests/HandBuiltKerberosSourcesTests.cs:86`

Location: `Curl.Engine.UnitTests/HandBuiltKerberosSourcesTests.cs:86`

The test calls Sources(...).CreateKdcClient(KerberosConfiguration.Empty) and asserts only Assert.IsNotNull(client). A non-nullable factory result cannot be null, so the test cannot fail unless it throws. Nothing checks that the client uses the configuration or the sources given.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/HandBuiltKerberosSourcesTests.cs -Pattern 'CreateKdcClient_Configuration_MakesAClient' -Context 0,9
```

- Expected: An assertion on how the client behaves with the configuration (for example which KDC it would contact).
- Actual: The only assertion is Assert.IsNotNull(client);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
