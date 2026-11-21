---
id: AF-0921
title: AuthenticateAsClientAsync_WithSessionCache_TracksTheConnectionsTickets uses a server that sends no ticket and asserts the cache is empty
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Networking.UnitTests/HandBuiltTlsProviderTests.SslSessions.cs:AuthenticateAsClientAsync_WithSessionCache_TracksTheConnectionsTickets:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-20
found-at: e78a1e70bbba7541cf5f1abfedb0ef1c8ae8e2a0
scorecard: 2026-11-20_1740.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0921 - AuthenticateAsClientAsync_WithSessionCache_TracksTheConnectionsTickets uses a server that sends no ticket and asserts the cache is empty

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitTests/HandBuiltTlsProviderTests.SslSessions.cs:51`: AuthenticateAsClientAsync_WithSessionCache_TracksTheConnectionsTickets uses a server that sends no ticket and asserts the cache is empty.

## Evidence

Location: `Curl.Networking.UnitTests/HandBuiltTlsProviderTests.SslSessions.cs:51`

The arrange step says `Diagnostics.Arrange("server", "TLS 1.3 sends no ticket");` and the only outcome check is line 51 `Assert.AreEqual(string.Empty, sessions.Export(), "the test server sends no ticket");`. The name promises that the provider tracks the connection's tickets, but a provider that ignored the session cache entirely, or dropped every ticket, passes the same way: the test never sees a ticket to track.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/HandBuiltTlsProviderTests.SslSessions.cs -Pattern 'sends no ticket'
```

- Expected: No match: the test server sends a NewSessionTicket and the test asserts the cache exports it.
- Actual: Two matches (lines 37 and 51): the server sends no ticket and the test asserts the export is empty.

## Re-audits

## Log

- 2026-11-20: filed proposed.
