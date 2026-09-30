---
id: BL-666
title: Add Roadmap milestones for phases 2, 3 and 5 from the tasks filed for them
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Roadmap.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-666 — Add Roadmap milestones for phases 2, 3 and 5 from the tasks filed for them

## Goal

`Documentation/Planning/Roadmap.md` has a milestone after Milestone 1 for each of phases 2 (`Ftp`, `Ssh`), 3 (`Smtp`, `Imap`, `Pop3`) and 5 (`Ldap`, `Smb`, `Rtsp`), plus the WebSocket part of phase 4, each with status, what it delivers, exit criteria and the task IDs on the board that deliver it, and the conformance-gap work (the options and `-w` fixes filed on 2026-09-28) placed in a milestone or the "Later" list.

## Context

- Conformance audit 2026-09-28, row 49 (Minor): the Roadmap sequences nothing after Milestone 1 while the Product Overview's phasing table plans phases 2 to 6.
- The tasks: SSH BL-560 to BL-578; mail BL-533 to BL-559 (with SASL BL-536 to BL-538); WebSocket BL-579 to BL-584; LDAP BL-585 to BL-589; RTSP BL-590 to BL-593; SMB BL-594 to BL-598; HTTP/2 BL-655 to BL-660. FTP is already built (see `Tasks/Done` archives). The Roadmap keeps its rule: no dates, ordered by dependency, and the board, not the Roadmap, holds each task's status.
- "Say what it does": state intent as intent; do not describe planned protocols as present.

## Acceptance criteria

- [x] `Documentation/Planning/Roadmap.md` has Milestone 2 (and later) sections with Status, Delivers (task IDs), Exit criteria and Outstanding, covering phases 2, 3 and 5 and the WebSocket work.
- [x] The opening paragraph no longer says later phases are not sequenced.
- [x] Every task ID cited exists on the board or in its archive.

## Notes

- Done directly rather than through align-and-document: a single-document edit with every fact checked against the board. Milestone 4 is the WebSocket part of Phase 4 and Milestone 6 is Phase 6; the 2026-09-28 options and -w fixes (BL-488 to BL-528, BL-599 to BL-654, BL-661) and HTTP/2 and HTTP/3 went into Milestone 6 rather than "Later", since Phase 6 is the conformance push.
- The stale "Later" HTTP/2 item (ADR-0017, superseded by ADR-0141 and ADR-0144) was removed; the zstd item now names the built decoder and BL-861.
- Every cited ID was checked against Tasks/ with a grep over the board; none missing. BL-806 names two live tasks, so the Roadmap does not cite it.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Roadmap sequences Milestones 2 to 6 (FTP and SSH, mail, WebSocket, LDAP/SMB/RTSP, conformance with HTTP/2 and HTTP/3) with their task IDs
