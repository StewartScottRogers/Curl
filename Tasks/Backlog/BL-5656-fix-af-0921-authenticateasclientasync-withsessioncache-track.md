---
id: BL-5656
title: Fix AF-0921: AuthenticateAsClientAsync_WithSessionCache_TracksTheConnectionsTickets uses a server that sends no ticket and asserts the cache is empty
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests/HandBuiltTlsProviderTests.SslSessions.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5656 — Fix AF-0921: AuthenticateAsClientAsync_WithSessionCache_TracksTheConnectionsTickets uses a server that sends no ticket and asserts the cache is empty

## Goal

The defect the audit office reported as AF-0921 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0921 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitTests/HandBuiltTlsProviderTests.SslSessions.cs:51`

Location: `Curl.Networking.UnitTests/HandBuiltTlsProviderTests.SslSessions.cs:51`

The arrange step says `Diagnostics.Arrange("server", "TLS 1.3 sends no ticket");` and the only outcome check is line 51 `Assert.AreEqual(string.Empty, sessions.Export(), "the test server sends no ticket");`. The name promises that the provider tracks the connection's tickets, but a provider that ignored the session cache entirely, or dropped every ticket, passes the same way: the test never sees a ticket to track.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/HandBuiltTlsProviderTests.SslSessions.cs -Pattern 'sends no ticket'
```

- Expected: No match: the test server sends a NewSessionTicket and the test asserts the cache exports it.
- Actual: Two matches (lines 37 and 51): the server sends no ticket and the test asserts the export is empty.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-20: Created.
