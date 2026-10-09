---
id: AF-0123
title: AuthenticateAsClientAsync_SchannelBuildOverTls13_ReportsOnlyReceivedTicketsAndKeepsTheData passes when no ticket is reported at all
auditor: quality
severity: Low
status: accepted
reason: 
key: quality:Curl.Networking.UnitTests/SslStreamTlsProviderTests.SessionTickets.cs:AuthenticateAsClientAsync_SchannelBuildOverTls13_ReportsOnlyReceivedTicketsAndKeepsTheData:weak-assertion
reproduction: none
task: BL-1873
tasks: BL-1873
found: 2026-10-08
found-at: cddb276d1d10fbb372f36a32cc1f588fd84c58e8
scorecard: 2026-10-08_2315.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0123 - AuthenticateAsClientAsync_SchannelBuildOverTls13_ReportsOnlyReceivedTicketsAndKeepsTheData passes when no ticket is reported at all

## Summary

Low finding from the quality auditor at `Curl.Networking.UnitTests/SslStreamTlsProviderTests.SessionTickets.cs:28`: AuthenticateAsClientAsync_SchannelBuildOverTls13_ReportsOnlyReceivedTicketsAndKeepsTheData passes when no ticket is reported at all. Reported by an auditor flagged unreliable in 2026-10-08_2315.md.

## Evidence

Location: `Curl.Networking.UnitTests/SslStreamTlsProviderTests.SessionTickets.cs:28`

The ticket assertion is 'Assert.IsTrue(messages.TrueForAll(message => !message.Sent && message.Bytes.Span[0] == 4));'. TrueForAll is true for an empty list, so a provider that stopped reporting received NewSessionTicket messages entirely would pass. The name says it reports them, and the TLS 1.2 sibling (ReportsNoTicket) is what an empty list should mean.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/SslStreamTlsProviderTests.SessionTickets.cs -Pattern 'TrueForAll'
```

- Expected: An assertion that at least one ticket was reported beside the TrueForAll.
- Actual: Lines 26 and 28: only TrueForAll(...) over messages, which an empty list satisfies.

## Re-audits

- 2026-10-09 | 2026-10-09_0225.md | reproduces: yes | Select-String shows SslStreamTlsProviderTests.SessionTickets.cs:28 Assert.IsTrue(messages.TrueForAll(message => !message.Sent && message.Bytes.Span[0] == 4)); with no count check. TrueForAll on an empty list is true, so a run reporting no ticket passes.
- 2026-10-09 | 2026-10-09_0647.md | reproduces: no | Ran the Select-String reproduction: the Windows test (OSCondition Windows), now named ..._ReportsTheReceivedTicketsAndKeepsTheData, asserts Assert.IsNotEmpty(messages) before the TrueForAll check (SslStreamTlsProviderTests.SessionTickets.cs:32-33). The off-Windows twin that accepts an empty list is now honestly named ..._ReportsNothingButReceivedTicketsAndKeepsTheData.

## Log

- 2026-10-08: filed proposed.
- 2026-10-09: proposed -> accepted.
