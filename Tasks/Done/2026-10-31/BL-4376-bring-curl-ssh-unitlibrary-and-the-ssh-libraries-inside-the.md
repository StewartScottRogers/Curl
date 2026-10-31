---
id: BL-4376
title: Bring Curl.Ssh.UnitLibrary and the SSH libraries inside the four quality gates
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4320, BL-4375]
touches: [Curl.Ssh.UnitLibrary, Curl.Ssh.UnitTests, Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Curl.Conformance.SshServer.UnitLibrary, Curl.Conformance.SshServer.UnitTests]
requirement: none
created: 2026-10-30
completed: 2026-10-30
---
# BL-4376 — Bring Curl.Ssh.UnitLibrary and the SSH libraries inside the four quality gates

## Goal

`Curl.Ssh.UnitLibrary`, `Curl.Protocol.Ssh.UnitLibrary` and `Curl.Conformance.SshServer.UnitLibrary` meet the four quality gates after the SSH layer move.

## Context

Split from BL-4362 (over its cost cap). Runs after the move lands (BL-4375). The previous run measured 0 failing members across the three assemblies; this task confirms it on the landed tree and fixes any member outside a gate.

## Acceptance criteria

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Ssh.UnitLibrary,Curl.Protocol.Ssh.UnitLibrary,Curl.Conformance.SshServer.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Split from BL-4362.
- 2026-10-30 lane 1: measured on the landed tree (after BL-4375): Curl.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitLibrary and Curl.Conformance.SshServer.UnitLibrary all at 100% line and branch coverage, max complexity 10, 0 failing members. No code change was needed. Build clean; fast tests green on Windows (113 test projects, 0 failures).

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 19ab4d29 3d836d5e.
- 2026-10-30: Doing -> Done. The three SSH libraries measure 0 failing members across the four quality gates on the landed tree
