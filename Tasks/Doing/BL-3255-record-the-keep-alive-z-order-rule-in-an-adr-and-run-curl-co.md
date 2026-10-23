---
id: BL-3255
title: Record the keep-alive -Z order rule in an ADR and run Curl.Console's quality gates
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-3253, BL-3254]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-10-23
completed:
---
# BL-3255 — Record the keep-alive -Z order rule in an ADR and run Curl.Console's quality gates

## Goal

The keep-alive extension of the -Z start-order rule is recorded in an ADR, and Curl.Console is inside every quality gate.

## Context

Split from BL-3189. Measured against curl 8.22.0 with a keep-alive loopback server: at `--parallel-max` 50 URL order every run (the pending transfer reuses the first connection); at 2 it varies; at 1 URL order. So the pending transfer goes last only when the host's first connection was closed. Write a new ADR amending ADR-0768 (in `Documentation/Planning/Decisions`), marked "Decided by Claude under Stewart's delegation". Then run `Measure-CodeQuality.ps1` for Curl.Console once (30 to 45 minutes). If a member falls outside a gate, file a follow-up task; never change a threshold.

## Acceptance criteria

- [ ] A new ADR amending ADR-0768 states the keep-alive extension and the measured orders, marked "Decided by Claude under Stewart's delegation".
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` shows Curl.Console inside every gate.
- [ ] `dotnet build Curl.Console -warnaserror` is clean and `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3189 (over its cost cap).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 31901186 6a0116a9 4c1375d3.
