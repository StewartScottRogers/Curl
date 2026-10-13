---
id: AF-0188
title: Ws CLAUDE.md tells the reader to take IConnection; WsProtocolHandler takes IConnector
auditor: truthfulness
severity: Medium
status: accepted
reason: 
key: truthfulness:Curl.Protocol.Ws.UnitLibrary/CLAUDE.md:IConnection:false-statement
reproduction: none
task: BL-2147
tasks: BL-2147
found: 2026-10-13
found-at: c86d04eeb8477108eb6d9195bb036a4ddaecc5fc
scorecard: 2026-10-13_0925.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0188 - Ws CLAUDE.md tells the reader to take IConnection; WsProtocolHandler takes IConnector

## Summary

Medium finding from the truthfulness auditor at `Curl.Protocol.Ws.UnitLibrary/CLAUDE.md:13`: Ws CLAUDE.md tells the reader to take IConnection; WsProtocolHandler takes IConnector. Reported by an auditor flagged unreliable in 2026-10-13_0925.md.

## Evidence

Location: `Curl.Protocol.Ws.UnitLibrary/CLAUDE.md:13`

Line 13-15: 'Never construct a Socket, SslStream or HttpClient here. Take `IConnection` so the tests in the matching .UnitTests project can drive this code from a recorded byte stream with no network.' WsProtocolHandler.cs:45-48 is 'public sealed class WsProtocolHandler(IConnector connector, IHttpAuthenticator authenticator, IWebSocketRandomSource randomSource)': it takes an IConnector and asks it for a connection per transfer. It takes no IConnection. The sibling Curl.Protocol.Gopher.UnitLibrary/CLAUDE.md states the real pattern: 'Seam: IConnector (ADR-0005) ... it never takes an IConnection in its constructor.' An agent following the Ws rule would give the handler an IConnection constructor parameter, against the seam the code uses.

## Reproduction

Run from the repository root:

```powershell
"$((Select-String -Path Curl.Protocol.Ws.UnitLibrary/CLAUDE.md -Pattern 'Take `IConnection`').Count);$((Select-String -Path Curl.Protocol.Ws.UnitLibrary/WsProtocolHandler.cs -Pattern '^\s+IConnector connector,').Count)"
```

- Expected: 0;1: the CLAUDE.md names IConnector, the seam the handler takes.
- Actual: 1;1

## Re-audits

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
