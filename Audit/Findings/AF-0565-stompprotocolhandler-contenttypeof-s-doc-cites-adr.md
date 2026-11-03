---
id: AF-0565
title: StompProtocolHandler.ContentTypeOf's doc cites ADR-1210 (Socket.IO pings) for the -H content-type rule that ADR-1220 decides
auditor: truthfulness
severity: Medium
status: proposed
reason:
key: truthfulness:Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs:ContentTypeOf:false-doc-comment
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
# AF-0565 - StompProtocolHandler.ContentTypeOf's doc cites ADR-1210 (Socket.IO pings) for the -H content-type rule that ADR-1220 decides

## Summary

Medium finding from the truthfulness auditor at `Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs:70`: StompProtocolHandler.ContentTypeOf's doc cites ADR-1210 (Socket.IO pings) for the -H content-type rule that ADR-1220 decides. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs:70`

The summary reads 'Gives the content-type for the SEND frame (ADR-1210): the value of the last -H "content-type: ..."...'. ADR-1210 is 'Socket.IO ends with exit 56 when no ping arrives within pingInterval + pingTimeout' and says nothing about STOMP. The rule the method implements (last -H content-type wins, name matched without regard to case, empty value ignored) is ADR-1220 'STOMP SEND takes the content-type a -H header gives', whose Consequences name StompProtocolHandler.ContentTypeOf. An agent following the citation reads an unrelated decision. Also, StompClientFlow.cs:47 still says SendContentType is the content-type 'every SEND frame carries (ADR-1209)', but since ADR-1220 it is only the default.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs -Pattern 'SEND</c> frame \(ADR-\d+\)'; Get-Content (Get-ChildItem Documentation/Planning/Decisions/ADR-1210-*.md).FullName -TotalCount 1
```

- Expected: The doc comment cites ADR-1220, the STOMP content-type decision.
- Actual: StompProtocolHandler.cs:70 cites (ADR-1210), whose title is '# ADR-1210: Socket.IO ends with exit 56 when no ping arrives within pingInterval + pingTimeout'.

## Re-audits

## Log

- 2026-11-03: filed proposed.
