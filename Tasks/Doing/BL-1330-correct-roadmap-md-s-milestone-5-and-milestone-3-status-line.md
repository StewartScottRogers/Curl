---
id: BL-1330
title: Correct Roadmap.md's Milestone 2 and Milestone 5 status lines: SMB, SCP and SFTP are registered
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Roadmap.md]
requirement: none
created: 2026-10-03
completed:
---
# BL-1330 — Correct Roadmap.md's Milestone 2 and Milestone 5 status lines: SMB, SCP and SFTP are registered

## Goal

The **Status** lines of Milestone 2 and Milestone 5 in `Documentation/Planning/Roadmap.md` say what is true of the code now: SCP, SFTP and SMB are built and registered in `Curl.Console`.

## Context

- `Documentation/Planning/Roadmap.md`, "Milestone 5 — Phase 5: LDAP, SMB and RTSP", says: "LDAP and RTSP are built and registered; SMB is built but not yet registered in `Curl.Console`." That is false: `Curl.Console/CurlComposition.cs` registers `new SmbProtocolHandler(recordingConnector)` (line 159) for `smb` and `smbs` (its doc comment, line 55, ADR-0200), and BL-598 ("register the SMB handler for smb and smbs in Curl.Console") is in `Tasks/Done/2026-10-01_1821/`. Every task Milestone 5 lists under "Delivers" (BL-585 to BL-598, BL-830, BL-840, BL-845, BL-853) is in a `Tasks/Done` archive.
- "Milestone 2 — Phase 2: FTP and SSH" says: "FTP is built and registered; SCP and SFTP are being built." `CurlComposition.cs` registers `scp` and `sftp` (doc comment line 56), and `Curl.Console.UnitTests/CurlCompositionSshVerboseTests.cs` runs them through the console.
- Whether a milestone is "Done" is set by its own "Exit criteria" and "Outstanding" lines: say "In progress" still if any "Delivers" task is not in `Tasks/Done` or its archives, and name what is outstanding; do not mark a milestone done that the exit criteria do not support. This task changes no other section.

## Acceptance criteria

- [ ] Milestone 5's **Status** line says LDAP, RTSP and SMB (`smb`, `smbs`) are built and registered in `Curl.Console`, and no longer says SMB is not registered.
- [ ] Milestone 2's **Status** line says FTP, SCP and SFTP are built and registered, and no longer says SCP and SFTP are being built.
- [ ] Each status line still says "In progress" or "Done" according to its milestone's "Delivers" tasks (checked with `task-board.ps1 status` and the `Tasks/Done` archives), and the Notes record which.
- [ ] No file other than `Documentation/Planning/Roadmap.md` changes.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
