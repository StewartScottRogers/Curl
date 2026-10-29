---
id: BL-4226
title: Fix AF-0464: ADR-0982 (Accepted, no amendment note) says GetWaitSockets keeps the FirstTransferSocket number for a transfer without a dialled socket; that number no longer exists
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-10-29
completed:
---
# BL-4226 — Fix AF-0464: ADR-0982 (Accepted, no amendment note) says GetWaitSockets keeps the FirstTransferSocket number for a transfer without a dialled socket; that number no longer exists

## Goal

The defect the audit office reported as AF-0464 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0464 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0464-adr-0982-accepted-no-amendment-note-says-getwaitso.md`.

Location: `Documentation/Planning/Decisions/ADR-0982-waitfds-names-each-transfers-real-socket.md:22`

Location: `Documentation/Planning/Decisions/ADR-0982-waitfds-names-each-transfers-real-socket.md:22`

ADR-0982 Decision lines 21-22: "Until the dial has a socket, and for a transfer that is not a `ManagedEasyTransfer`, it keeps the `FirstTransferSocket` number." Curl.Engine.UnitLibrary/ManagedMultiTransfer.WaitSockets.cs:136-143 doc and code: WaitedSockets gives "each running transfer's known socket (none for one that ... has no socket the handle knows)". FirstTransferSocket appears in no non-test source file. ADR-0997 says it is gone, but ADR-0982's status is still plain "Accepted".

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-0982-*.md, Curl.Engine.UnitLibrary/ManagedMultiTransfer.WaitSockets.cs -Pattern '`FirstTransferSocket` number', 'none for$'
```

- Expected: The ADR is marked replaced in part by ADR-0997, or WaitSockets still names a FirstTransferSocket number.
- Actual: ADR-0982-...md:22:   `FirstTransferSocket` number.; ManagedMultiTransfer.WaitSockets.cs:136: /// The sockets <see cref="GetWaitSockets" /> answers, in order: each running transfer's known socket (none for

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
