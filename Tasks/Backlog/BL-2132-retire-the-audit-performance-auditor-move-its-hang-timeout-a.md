---
id: BL-2132
title: Retire the audit performance auditor; move its hang, timeout and blow-up checks to another auditor
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Audit, .claude/agents, CLAUDE.md]
lane: no
requirement: none
created: 2026-10-13
completed:
---
# BL-2132 — Retire the audit performance auditor; move its hang, timeout and blow-up checks to another auditor

## Goal

No audit compares Curl's speed, memory or binary size with curl's any more. The
behaviour-breaking performance checks (a hang, a timeout, quadratic blow-up, a transfer that
fails under load) live on in another auditor's method.

## Context

Stewart, 2026-10-13: C curl will always be faster and smaller than any C# program, so Curl is
never to try to match or beat it on speed, memory or size. Only behaviour has to match. He chose
to retire the `audit-performance` auditor (Sonnet; six fixed transfers with a 1.25x limit
against real curl, e.g. AF-0048) and keep only checks for performance problems that break
behaviour.

Where the auditor is wired in:
- `.claude/agents/audit-performance.md` and `Audit/Instructions/Performance.md`
- `Audit/RunAudit.ps1` (the auditor list, `-Auditors` and its warning)
- `.claude/agents/audit-seeder.md` and `Audit/PlantedDefects/` (performance plants)
- `Audit/Instructions/Auditor-Rules.md`, `Report-Format.md` and `Process.md`
- `Audit/Tools/Measure-Performance.ps1`, the scorecard writer and catch-rate code
- `Audit/README.md`, and CLAUDE.md's "Audit office" list of six auditors

Audit paths and CLAUDE.md go through the `audit` branch: merge, never rebase.

Open accepted performance findings close as `closed-how: stewart` with this decision as the
reason. Stewart decided this in chat on 2026-10-13.

## Acceptance criteria

- [ ] `RunAudit.ps1` no longer runs a performance auditor; its auditor list, `-Auditors` help and its self-test agree; `-DryRun` shows five auditors.
- [ ] The hang, timeout and blow-up checks are in another auditor's method (`audit-conformance` or `audit-quality`, whichever fits), phrased as behaviour failures with no speed, memory or size ratio against curl.
- [ ] The seeder's catalogue holds no performance-ratio plants; any plant kept for the moved checks is assigned to that auditor.
- [ ] `audit-performance.md` and `Performance.md` are removed or marked retired, and every document naming six auditors or the performance auditor is updated, CLAUDE.md included.
- [ ] Every still-open accepted performance finding is closed `closed-how: stewart`, with the 2026-10-13 decision as its reason.
- [ ] `RunAudit.ps1 -SelfTest` passes, and the change lands through an `audit` branch PR with CI green on Windows, Linux and macOS.

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-10-13: Created.
