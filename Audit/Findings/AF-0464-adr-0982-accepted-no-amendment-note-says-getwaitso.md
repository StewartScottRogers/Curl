---
id: AF-0464
title: ADR-0982 (Accepted, no amendment note) says GetWaitSockets keeps the FirstTransferSocket number for a transfer without a dialled socket; that number no longer exists
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-10-31_1149.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-31_0358.md, 2026-10-31_1149.md).
key: truthfulness:Documentation/Planning/Decisions/ADR-0982-waitfds-names-each-transfers-real-socket.md:FirstTransferSocket:stale-adr
reproduction: none
task: BL-4226
tasks: BL-4226
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed: 2026-10-31
closed-how: consecutive
closed-by: 2026-10-31_0358.md, 2026-10-31_1149.md
---
# AF-0464 - ADR-0982 (Accepted, no amendment note) says GetWaitSockets keeps the FirstTransferSocket number for a transfer without a dialled socket; that number no longer exists

## Summary

Low finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-0982-waitfds-names-each-transfers-real-socket.md:22`: ADR-0982 (Accepted, no amendment note) says GetWaitSockets keeps the FirstTransferSocket number for a transfer without a dialled socket; that number no longer exists.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-0982-waitfds-names-each-transfers-real-socket.md:22`

ADR-0982 Decision lines 21-22: "Until the dial has a socket, and for a transfer that is not a `ManagedEasyTransfer`, it keeps the `FirstTransferSocket` number." Curl.Engine.UnitLibrary/ManagedMultiTransfer.WaitSockets.cs:136-143 doc and code: WaitedSockets gives "each running transfer's known socket (none for one that ... has no socket the handle knows)". FirstTransferSocket appears in no non-test source file. ADR-0997 says it is gone, but ADR-0982's status is still plain "Accepted".

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-0982-*.md, Curl.Engine.UnitLibrary/ManagedMultiTransfer.WaitSockets.cs -Pattern '`FirstTransferSocket` number', 'none for$'
```

- Expected: The ADR is marked replaced in part by ADR-0997, or WaitSockets still names a FirstTransferSocket number.
- Actual: ADR-0982-...md:22:   `FirstTransferSocket` number.; ManagedMultiTransfer.WaitSockets.cs:136: /// The sockets <see cref="GetWaitSockets" /> answers, in order: each running transfer's known socket (none for

## Re-audits

- 2026-10-31 | 2026-10-31_0358.md | reproduces: no | Ran the reproduction. ADR-0982 no longer matches '`FirstTransferSocket` number'. Its status line says 'Its point on a transfer with no dialled socket is replaced in part by ADR-0997: `FirstTransferSocket` is gone.' The only match left is ManagedMultiTransfer.WaitSockets.cs:136 ('none for'), the code's own doc, which agrees.
- 2026-10-31 | 2026-10-31_1149.md | reproduces: no | Ran the reproduction: '`FirstTransferSocket` number' no longer matches ADR-0982; only ManagedMultiTransfer.WaitSockets.cs:136 matches. ADR-0982 line 4 now says it is replaced in part by ADR-0997 and that FirstTransferSocket is gone.

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
- 2026-10-31: accepted -> closed. Re-audit 2026-10-31_1149.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-31_0358.md, 2026-10-31_1149.md).
