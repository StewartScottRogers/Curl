---
id: AF-0565
title: StompProtocolHandler.ContentTypeOf's doc cites ADR-1210 (Socket.IO pings) for the -H content-type rule that ADR-1220 decides
auditor: truthfulness
severity: Medium
status: closed
reason: Re-audit 2026-11-03_1017.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs:ContentTypeOf:false-doc-comment
reproduction: none
task: BL-4713
tasks: BL-4713
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed: 2026-11-03
closed-how: reliable-reaudit
closed-by: 2026-11-03_1017.md
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

- 2026-11-03 | 2026-11-03_1017.md | reproduces: no | Ran the reproduction: StompProtocolHandler.cs:70 now reads 'Gives the content-type for the SEND frame (ADR-1220)', and ADR-1220-stomp-send-takes-the-content-type-a-h-header-gives.md exists. ADR-1210's first line is the Socket.IO ping ADR, which is no longer cited.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1017.md: the reproduction no longer reproduces.
