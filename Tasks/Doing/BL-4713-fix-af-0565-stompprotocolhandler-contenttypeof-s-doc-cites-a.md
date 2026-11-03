---
id: BL-4713
title: Fix AF-0565: StompProtocolHandler.ContentTypeOf's doc cites ADR-1210 (Socket.IO pings) for the -H content-type rule that ADR-1220 decides
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Extended.Protocol.Stomp.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4713 — Fix AF-0565: StompProtocolHandler.ContentTypeOf's doc cites ADR-1210 (Socket.IO pings) for the -H content-type rule that ADR-1220 decides

## Goal

The defect the audit office reported as AF-0565 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0565 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0565-stompprotocolhandler-contenttypeof-s-doc-cites-adr.md`.

Location: `Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs:70`

Location: `Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs:70`

The summary reads 'Gives the content-type for the SEND frame (ADR-1210): the value of the last -H "content-type: ..."...'. ADR-1210 is 'Socket.IO ends with exit 56 when no ping arrives within pingInterval + pingTimeout' and says nothing about STOMP. The rule the method implements (last -H content-type wins, name matched without regard to case, empty value ignored) is ADR-1220 'STOMP SEND takes the content-type a -H header gives', whose Consequences name StompProtocolHandler.ContentTypeOf. An agent following the citation reads an unrelated decision. Also, StompClientFlow.cs:47 still says SendContentType is the content-type 'every SEND frame carries (ADR-1209)', but since ADR-1220 it is only the default.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs -Pattern 'SEND</c> frame \(ADR-\d+\)'; Get-Content (Get-ChildItem Documentation/Planning/Decisions/ADR-1210-*.md).FullName -TotalCount 1
```

- Expected: The doc comment cites ADR-1220, the STOMP content-type decision.
- Actual: StompProtocolHandler.cs:70 cites (ADR-1210), whose title is '# ADR-1210: Socket.IO ends with exit 56 when no ping arrives within pingInterval + pingTimeout'.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Comment-only fix: StompProtocolHandler.cs:70 now cites ADR-1220; StompClientFlow.cs:47 says SendContentType is the default, overridden by -H (ADR-1220). Build clean, Stomp fast tests green. No code change, so no quality run.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
