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
completed:
---
# BL-4376 — Bring Curl.Ssh.UnitLibrary and the SSH libraries inside the four quality gates

## Goal

`Curl.Ssh.UnitLibrary`, `Curl.Protocol.Ssh.UnitLibrary` and `Curl.Conformance.SshServer.UnitLibrary` meet the four quality gates after the SSH layer move.

## Context

Split from BL-4362 (over its cost cap). Runs after the move lands (BL-4375). The previous run measured 0 failing members across the three assemblies; this task confirms it on the landed tree and fixes any member outside a gate.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Ssh.UnitLibrary,Curl.Protocol.Ssh.UnitLibrary,Curl.Conformance.SshServer.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Split from BL-4362.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 19ab4d29 3d836d5e.
