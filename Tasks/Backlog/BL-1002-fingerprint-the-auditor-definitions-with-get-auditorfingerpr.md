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
completed:
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

- [ ] Two runs on an unchanged tree print the same 64-character lowercase hex string.
- [ ] Editing `.claude/agents/audit-quality.md` (or, before it exists, adding a scratch `.claude/agents/audit-probe.md`) changes the output; reverting restores it.
- [ ] Adding a file under `Audit/Findings/` or `Audit/Scorecards/`, or editing `.claude/agents/code-reviewer.md`, leaves the output unchanged.
- [ ] Converting an included file between CRLF and LF line endings leaves the output unchanged.
- [ ] `-List` prints the included paths in ordinal order and then the hash.
- [ ] The same tree gives the same hash under `powershell` and `pwsh`.

## Notes

## Log

- 2026-09-29: Created.
