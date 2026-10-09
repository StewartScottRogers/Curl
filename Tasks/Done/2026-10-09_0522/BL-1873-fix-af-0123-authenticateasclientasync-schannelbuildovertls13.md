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
completed: 2026-10-09
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Measured on Windows: the Schannel test server sends one NewSessionTicket after a TLS 1.3 handshake ("received type 4"). Whether OpenSSL (Linux) or macOS servers send one could not be measured from a Windows lane, so the test is split by platform: the Windows test (renamed `..._ReportsTheReceivedTicketsAndKeepsTheData`) adds `Assert.IsNotEmpty(messages)` beside the TrueForAll; the non-Windows test (`..._ReportsNothingButReceivedTicketsAndKeepsTheData`) keeps only the TrueForAll, and its name now says exactly that an empty list passes.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. TLS 1.3 Schannel-build ticket test now asserts at least one ticket on Windows; off Windows its name says an empty list passes
