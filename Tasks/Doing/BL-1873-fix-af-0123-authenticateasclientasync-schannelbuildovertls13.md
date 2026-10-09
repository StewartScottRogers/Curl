---
id: BL-1873
title: Fix AF-0123: AuthenticateAsClientAsync_SchannelBuildOverTls13_ReportsOnlyReceivedTicketsAndKeepsTheData passes when no ticket is reported at all
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1873 — Fix AF-0123: AuthenticateAsClientAsync_SchannelBuildOverTls13_ReportsOnlyReceivedTicketsAndKeepsTheData passes when no ticket is reported at all

## Goal

The defect the audit office reported as AF-0123 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0123 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0123-authenticateasclientasync-schannelbuildovertls13-r.md`.

Location: `Curl.Networking.UnitTests/SslStreamTlsProviderTests.SessionTickets.cs:28`

Location: `Curl.Networking.UnitTests/SslStreamTlsProviderTests.SessionTickets.cs:28`

The ticket assertion is 'Assert.IsTrue(messages.TrueForAll(message => !message.Sent && message.Bytes.Span[0] == 4));'. TrueForAll is true for an empty list, so a provider that stopped reporting received NewSessionTicket messages entirely would pass. The name says it reports them, and the TLS 1.2 sibling (ReportsNoTicket) is what an empty list should mean.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/SslStreamTlsProviderTests.SessionTickets.cs -Pattern 'TrueForAll'
```

- Expected: An assertion that at least one ticket was reported beside the TrueForAll.
- Actual: Lines 26 and 28: only TrueForAll(...) over messages, which an empty list satisfies.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
