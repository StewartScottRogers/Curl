---
id: AF-0529
title: For_GivesTheExtensionsTheRunsEnvironmentReader never checks that the reader reaches the extensions
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-03_1615.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1017.md, 2026-11-03_1615.md).
key: quality:Curl.Extension.Composition.UnitTests/ExtensionProtocolHandlersTests.cs:For_GivesTheExtensionsTheRunsEnvironmentReader:name-lies
reproduction: none
task: BL-4691
tasks: BL-4635, BL-4691
found: 2026-11-02
found-at: fcdba3d03e8b5378439deb78f2055b6f5eccf2bb
scorecard: 2026-11-02_1724.md
duplicate-of:
closed: 2026-11-03
closed-how: consecutive
closed-by: 2026-11-03_1017.md, 2026-11-03_1615.md
---
# AF-0529 - For_GivesTheExtensionsTheRunsEnvironmentReader never checks that the reader reaches the extensions

## Summary

Medium finding from the quality auditor at `Curl.Extension.Composition.UnitTests/ExtensionProtocolHandlersTests.cs:33`: For_GivesTheExtensionsTheRunsEnvironmentReader never checks that the reader reaches the extensions. Reported by an auditor flagged unreliable in 2026-11-02_1724.md.

## Evidence

Location: `Curl.Extension.Composition.UnitTests/ExtensionProtocolHandlersTests.cs:33`

The test's only assertions are Assert.HasCount(ExtensionCatalogue.Entries.Count, handlers) and Assert.IsNull(Services.ReadEnvironmentVariable), the latter checking the caller's own unchanged services record. Neither looks at the services the handler factories receive. Production ExtensionProtocolHandlers.For builds 'services => Create(services with { ReadEnvironmentVariable = readEnvironmentVariable })' (BL-4534). Replacing that with 'services => Create(services)' would drop the run's environment for every extension, and this test, whose name promises exactly that hand-over, would still pass. CreateFrom_BuildsEachHandlerOverTheServicesInOrder calls CreateFrom directly, not For, so nothing checks the hand-over.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.Composition.UnitTests/ExtensionProtocolHandlersTests.cs -Pattern 'For_GivesTheExtensionsTheRunsEnvironmentReader' -Context 0,7
```

- Expected: An assertion that the services handed to each extension factory carry the given reader (Assert.AreSame(reader, seen.ReadEnvironmentVariable)).
- Actual: Only Assert.HasCount(ExtensionCatalogue.Entries.Count, handlers); and Assert.IsNull(Services.ReadEnvironmentVariable);

## Re-audits

- 2026-11-03 | 2026-11-03_0025.md | reproduces: yes | The test body is unchanged: 'Assert.HasCount(ExtensionCatalogue.Entries.Count, handlers); Assert.IsNull(Services.ReadEnvironmentVariable);'. It still never checks that the reader reaches the extensions. A separate test, For_HandsEachExtensionFactoryServicesCarryingTheGivenReader, now covers the behaviour, but this test's name still promises what its body does not check.
- 2026-11-03 | 2026-11-03_1017.md | reproduces: no | Select-String context now shows Assert.AreSame(reader, seen!.ReadEnvironmentVariable); after the For call: the reader is checked to reach the extensions.
- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Line 40 now asserts Assert.AreSame(reader, seen!.ReadEnvironmentVariable): the reader reaching the extensions is checked.

## Log

- 2026-11-02: filed proposed.
- 2026-11-02: proposed -> accepted.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1017.md, 2026-11-03_1615.md).
