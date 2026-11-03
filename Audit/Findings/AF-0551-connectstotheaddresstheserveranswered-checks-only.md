---
id: AF-0551
title: ConnectsToTheAddressTheServerAnswered checks only that a connection exists
auditor: quality
severity: Low
status: accepted
reason: 
key: quality:Curl.Engine.IntegrationTests/EngineCompositionDnsServersIntegrationTests.cs:CreateTransports_WithDnsServersOnLoopback_ConnectsToTheAddressTheServerAnswered:weak-assertion
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
# AF-0551 - ConnectsToTheAddressTheServerAnswered checks only that a connection exists

## Summary

Low finding from the quality auditor at `Curl.Engine.IntegrationTests/EngineCompositionDnsServersIntegrationTests.cs:45`: ConnectsToTheAddressTheServerAnswered checks only that a connection exists. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Engine.IntegrationTests/EngineCompositionDnsServersIntegrationTests.cs:45`

'Assert.IsNotNull(result.Connection, result.ErrorMessage);' is the test's only assertion; the dialled address and whether the DNS server was asked are never compared.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.IntegrationTests/EngineCompositionDnsServersIntegrationTests.cs -Pattern 'Assert\.'
```

- Expected: Assertions on the dialled end point and the query the server received.
- Actual: One line: Assert.IsNotNull(result.Connection, result.ErrorMessage);

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
