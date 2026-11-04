---
id: AF-0551
title: ConnectsToTheAddressTheServerAnswered checks only that a connection exists
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-11-03_1615.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1017.md, 2026-11-03_1615.md).
key: quality:Curl.Engine.IntegrationTests/EngineCompositionDnsServersIntegrationTests.cs:CreateTransports_WithDnsServersOnLoopback_ConnectsToTheAddressTheServerAnswered:weak-assertion
reproduction: none
task: BL-4699
tasks: BL-4699
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed: 2026-11-03
closed-how: consecutive
closed-by: 2026-11-03_1017.md, 2026-11-03_1615.md
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

- 2026-11-03 | 2026-11-03_1017.md | reproduces: no | Beside Assert.IsNotNull(result.Connection, ...) the test now asserts Assert.AreEqual(IPAddress.Loopback, dialled.Address) and Assert.AreEqual(webPort, dialled.Port).
- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Asserts now include Assert.AreEqual(IPAddress.Loopback, dialled.Address) and Assert.AreEqual(webPort, dialled.Port).

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1017.md, 2026-11-03_1615.md).
