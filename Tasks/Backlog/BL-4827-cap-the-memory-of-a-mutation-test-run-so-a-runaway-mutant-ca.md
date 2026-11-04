---
id: BL-4827
title: Cap the memory of a mutation test run so a runaway mutant cannot exhaust the machine
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Audit/Tools/Invoke-MutationTest.ps1]
lane: no
requirement: none
created: 2026-11-04
completed:
---
# BL-4827 — Cap the memory of a mutation test run so a runaway mutant cannot exhaust the machine

## Goal

A mutation test run whose test host grows past a memory limit is stopped and recorded as a killed mutant, before it can exhaust the machine.

## Context

Audit 20261104-064338, 2026-11-04 07:23 MST: the quality auditor's mutation run on `Curl.Mcp.Protocol.UnitTests` (`planted.audit\mutation-20261104-072333`) made testhost.exe allocate 80 GB of the machine's 126 GB in about 4 minutes, leaving 6 GB free; Claude Code reaped background shells for low memory, and an interactive session killed the process (pid 87344) by hand. `Invoke-MutationTest.ps1`'s `-TimeoutSeconds` (600) would have come too late. A runaway mutant may also have crashed an interactive session on 2026-11-02.

## Acceptance criteria

- [ ] `Audit/Tools/Invoke-MutationTest.ps1` watches the test run's process tree and stops it once any process in it passes a memory limit (a `-MaxMemoryGB` parameter, default 8), recording the mutant as killed with the reason `memory limit`.
- [ ] Its `-SelfTest` has a case with a stand-in that allocates past the limit and is stopped and recorded as killed, and passes under Windows PowerShell 5.1 and pwsh 7.

## Notes

## Log

- 2026-11-04: Created.
