---
id: BL-2147
title: Fix AF-0188: Ws CLAUDE.md tells the reader to take IConnection; WsProtocolHandler takes IConnector
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Protocol.Ws.UnitLibrary]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2147 — Fix AF-0188: Ws CLAUDE.md tells the reader to take IConnection; WsProtocolHandler takes IConnector

## Goal

The defect the audit office reported as AF-0188 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0188 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0188-ws-claude-md-tells-the-reader-to-take-iconnection.md`.

Location: `Curl.Protocol.Ws.UnitLibrary/CLAUDE.md:13`

Location: `Curl.Protocol.Ws.UnitLibrary/CLAUDE.md:13`

Line 13-15: 'Never construct a Socket, SslStream or HttpClient here. Take `IConnection` so the tests in the matching .UnitTests project can drive this code from a recorded byte stream with no network.' WsProtocolHandler.cs:45-48 is 'public sealed class WsProtocolHandler(IConnector connector, IHttpAuthenticator authenticator, IWebSocketRandomSource randomSource)': it takes an IConnector and asks it for a connection per transfer. It takes no IConnection. The sibling Curl.Protocol.Gopher.UnitLibrary/CLAUDE.md states the real pattern: 'Seam: IConnector (ADR-0005) ... it never takes an IConnection in its constructor.' An agent following the Ws rule would give the handler an IConnection constructor parameter, against the seam the code uses.

Reproduction, from the finding:

Run from the repository root:

```powershell
"$((Select-String -Path Curl.Protocol.Ws.UnitLibrary/CLAUDE.md -Pattern 'Take `IConnection`').Count);$((Select-String -Path Curl.Protocol.Ws.UnitLibrary/WsProtocolHandler.cs -Pattern '^\s+IConnector connector,').Count)"
```

- Expected: 0;1: the CLAUDE.md names IConnector, the seam the handler takes.
- Actual: 1;1

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Ws CLAUDE.md now names the IConnector seam (ADR-0005), as Gopher's does. Docs only; no code changed.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. Ws CLAUDE.md names IConnector; reproduction gives 0;1
