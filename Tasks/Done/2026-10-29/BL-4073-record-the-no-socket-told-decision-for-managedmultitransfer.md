---
id: BL-4073
title: Record the no-socket-told decision for ManagedMultiTransfer and update the Engine CLAUDE.md
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-4072]
touches: [Documentation/Planning/Decisions/README.md, Curl.Engine.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4073 — Record the no-socket-told decision for ManagedMultiTransfer and update the Engine CLAUDE.md

## Goal

The decision that a transfer with no socket the handle knows is told none and left out of waitfds is recorded in an ADR, and the Engine library's CLAUDE.md matches the code after BL-4072.

## Context

Documentation half of BL-4068 (CGR-034). Real libcurl has no such transfer, so there is nothing to measure. Read `Documentation/Planning/Decisions/README.md` for the next free ADR number and the row format.

## Acceptance criteria

- [x] A new ADR, marked "Decided by Claude under Stewart's delegation", states that a transfer with no socket the handle knows is told no socket and is left out of `GetWaitSockets`, and why; its row is in `Documentation/Planning/Decisions/README.md`.
- [x] The BL-2426/BL-2427 paragraphs in `Curl.Engine.UnitLibrary/CLAUDE.md` no longer mention `FirstTransferSocket` or synthetic socket numbers and keep the 10 ms interval for BL-4069.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no `Curl.Engine.UnitLibrary` member outside a gate, and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-4068 (cost cap). The new ADR file is a new path under `Documentation/Planning/Decisions/`; BL-4069 also touches the Engine `CLAUDE.md`, so they run apart.

## Log

- Notes: ADR-0997 filed. Fast tests not rerun: this task changed only Markdown; BL-4072 (code) ran them. Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary: 0 failing members.

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 6b40543d 2201ed3c 23a3c95f.
- 2026-10-29: Doing -> Done. ADR-0997 recorded and Engine CLAUDE.md matches the no-socket-told code
