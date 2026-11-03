---
id: BL-4691
title: Re-fix AF-0529: For_GivesTheExtensionsTheRunsEnvironmentReader never checks that the reader reaches the extensions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Composition.UnitTests]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4691 — Re-fix AF-0529: For_GivesTheExtensionsTheRunsEnvironmentReader never checks that the reader reaches the extensions

## Goal

The defect the audit office reported as AF-0529 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0529 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0529-for-givestheextensionstherunsenvironmentreader-nev.md`.

Re-fix: the earlier task(s) BL-4635 reached Done, and a later re-audit by the quality auditor found the reproduction still reproduces:

- 2026-11-03 | 2026-11-03_0025.md | reproduces: yes | The test body is unchanged: 'Assert.HasCount(ExtensionCatalogue.Entries.Count, handlers); Assert.IsNull(Services.ReadEnvironmentVariable);'. It still never checks that the reader reaches the extensions. A separate test, For_HandsEachExtensionFactoryServicesCarryingTheGivenReader, now covers the behaviour, but this test's name still promises what its body does not check.

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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Test-only change, so done directly rather than through the full feature pipeline, and no Measure-CodeQuality run: no production code changed. For_GivesTheExtensionsTheRunsEnvironmentReader now drives For through a recording factory and asserts Assert.AreSame(reader, seen!.ReadEnvironmentVariable), with the assertion on line 40, inside the reproduction's 7-line context. The duplicate For_HandsEachExtensionFactoryServicesCarryingTheGivenReader was folded into it. The catalogue count check it used to make is now For_WithTheCatalogue_GivesOneHandlerPerEntry.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. For_GivesTheExtensionsTheRunsEnvironmentReader asserts the extensions' services carry the given reader
