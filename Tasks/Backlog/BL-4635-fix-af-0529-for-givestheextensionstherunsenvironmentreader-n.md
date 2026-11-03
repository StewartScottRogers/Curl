---
id: BL-4635
title: Fix AF-0529: For_GivesTheExtensionsTheRunsEnvironmentReader never checks that the reader reaches the extensions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Composition.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4635 — Fix AF-0529: For_GivesTheExtensionsTheRunsEnvironmentReader never checks that the reader reaches the extensions

## Goal

The defect the audit office reported as AF-0529 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0529 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0529-for-givestheextensionstherunsenvironmentreader-nev.md`.

Location: `Curl.Extension.Composition.UnitTests/ExtensionProtocolHandlersTests.cs:33`

Location: `Curl.Extension.Composition.UnitTests/ExtensionProtocolHandlersTests.cs:33`

The test's only assertions are Assert.HasCount(ExtensionCatalogue.Entries.Count, handlers) and Assert.IsNull(Services.ReadEnvironmentVariable), the latter checking the caller's own unchanged services record. Neither looks at the services the handler factories receive. Production ExtensionProtocolHandlers.For builds 'services => Create(services with { ReadEnvironmentVariable = readEnvironmentVariable })' (BL-4534). Replacing that with 'services => Create(services)' would drop the run's environment for every extension, and this test, whose name promises exactly that hand-over, would still pass. CreateFrom_BuildsEachHandlerOverTheServicesInOrder calls CreateFrom directly, not For, so nothing checks the hand-over.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.Composition.UnitTests/ExtensionProtocolHandlersTests.cs -Pattern 'For_GivesTheExtensionsTheRunsEnvironmentReader' -Context 0,7
```

- Expected: An assertion that the services handed to each extension factory carry the given reader (Assert.AreSame(reader, seen.ReadEnvironmentVariable)).
- Actual: Only Assert.HasCount(ExtensionCatalogue.Entries.Count, handlers); and Assert.IsNull(Services.ReadEnvironmentVariable);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
