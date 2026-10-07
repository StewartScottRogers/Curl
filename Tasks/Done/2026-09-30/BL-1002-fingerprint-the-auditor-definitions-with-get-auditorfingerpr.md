---
id: BL-1002
title: Fingerprint the auditor definitions with Get-AuditorFingerprint.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1000]
touches: [Audit/Tools/Get-AuditorFingerprint.ps1]
lane: no
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-1002 — Fingerprint the auditor definitions with Get-AuditorFingerprint.ps1

## Goal

`Audit/Tools/Get-AuditorFingerprint.ps1` prints one SHA-256 hash that changes whenever any auditor definition, auditor instruction or audit tool changes and never otherwise, so each scorecard records exactly which auditors produced it.

## Context

Interactive only (`lane: no`): it writes `Audit/`. Run it with `/task-run BL-1002`.
The ADR from BL-994 requires every scorecard to record a fingerprint of the auditor
definitions it ran with; BL-1017 writes it into the scorecard.

Design:

- Inputs, found under `-Root` (default: the repository root above the script): every
  file matching `.claude/agents/audit-*.md`, and every file under `Audit/Instructions/`
  and `Audit/Tools/` (recursively). Not `Audit/Findings`, `Audit/Scorecards`,
  `Audit/PlantedDefects` or anything else: the fingerprint identifies the auditors, not
  their output or the defects they are tested with.
- Order: repository-relative paths with `/` separators, sorted ordinally.
- Hash with `System.Security.Cryptography.SHA256` over, per file: the path's UTF-8 bytes,
  a `0x0A` byte, the file's bytes with every CRLF turned into LF, a `0x00` byte. CRLF
  normalisation makes the hash identical on Windows, Linux and macOS whatever
  `core.autocrlf` does.
- Output: the lowercase hex hash on one line. `-List` also prints each included path
  before it, for debugging.
- Runs under Windows PowerShell 5.1 and PowerShell 7; ASCII only.

## Acceptance criteria

- [x] Two runs on an unchanged tree print the same 64-character lowercase hex string.
- [x] Editing `.claude/agents/audit-quality.md` (or, before it exists, adding a scratch `.claude/agents/audit-probe.md`) changes the output; reverting restores it.
- [x] Adding a file under `Audit/Findings/` or `Audit/Scorecards/`, or editing `.claude/agents/code-reviewer.md`, leaves the output unchanged.
- [x] Converting an included file between CRLF and LF line endings leaves the output unchanged.
- [x] `-List` prints the included paths in ordinal order and then the hash.
- [x] The same tree gives the same hash under `powershell` and `pwsh`.

## Notes

- On the audit branch (worktree Z:/repos/Curl.audit), commit f0e5e56e, pull request https://github.com/StewartScottRogers/Curl/pull/30.
- CRLF -> LF uses a Latin-1 round trip (each byte one char and back unchanged), so it is one string replace, not a per-byte PowerShell loop, and works on any file, text or not.
- Tests against a scratch copy, 8 PASS / 0 FAIL: stable 64-char lowercase hex; adding .claude/agents/audit-probe.md changes it and removing restores it; Audit/Findings, Audit/Scorecards and code-reviewer.md do not; CRLF and LF agree; -List ordinal and auditor-only, then the hash; Windows PowerShell 5.1 and PowerShell 7.6.6 agree; ASCII only.
- PowerShell 7 was not on this PC: installed as a .NET global tool (dotnet tool install --global PowerShell, 7.6.6; remove with dotnet tool uninstall --global PowerShell). Machine-local, not a solution dependency.
- Fingerprint of the audit branch today: 2663c411beee12b0f9b99131a7c73c21ed19d221cf945579e38991cae5dcaf4a.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Get-AuditorFingerprint.ps1 prints one stable SHA-256 of the auditors; in PR #30, awaiting Stewart's merge.
