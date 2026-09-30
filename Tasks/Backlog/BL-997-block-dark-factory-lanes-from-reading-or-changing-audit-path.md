---
id: BL-997
title: Block dark factory lanes from reading or changing audit paths with a PreToolUse hook
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-995]
touches: [.claude/hooks/guard-audit-paths.ps1, .claude/settings.json]
lane: no
requirement: none
created: 2026-09-29
completed:
---
# BL-997 — Block dark factory lanes from reading or changing audit paths with a PreToolUse hook

## Goal

In any Claude Code session where `CURL_DARK_FACTORY_LANE` is set, an Edit, Write, MultiEdit or NotebookEdit of an audit path, or a Bash or PowerShell command that names one, is refused before it runs; sessions without the variable are unaffected.

## Context

Interactive only (`lane: no`): audit guard layer 2 (the ADR from BL-994). Run it with
`/task-run BL-997`.

Audit paths are `Audit/` and `.claude/agents/audit-*`. Lanes run `claude -p
--dangerously-skip-permissions`, so permission rules do not stop them; a PreToolUse hook
still runs in that mode, and exit code 2 blocks the tool call and shows its stderr to
the model. BL-995 sets the variable on every factory process; the hook process is a
child of Claude Code, so a lane's shell cannot unset it for the hook.

Conventions to follow: `.claude/hooks/format-cs.ps1` and
`.claude/hooks/whisper-milestone.ps1` - PowerShell, read the hook JSON from standard
input, registered in `.claude/settings.json` as
`powershell -NoProfile -ExecutionPolicy Bypass -File .claude/hooks/<name>.ps1`. ASCII
only (Windows PowerShell 5.1 reads BOM-less scripts in the system code page).

Design of `.claude/hooks/guard-audit-paths.ps1`:

- If `$env:CURL_DARK_FACTORY_LANE` is empty, `exit 0` before reading standard input.
- Otherwise read the JSON. `tool_name` Edit/Write/MultiEdit: `tool_input.file_path`;
  NotebookEdit: `tool_input.notebook_path`. Replace `\` with `/`; refuse when it matches
  `(?i)(^|/)Audit(/|$)` or `(?i)(^|/)\.claude/agents/audit-[^/]*$`.
- `tool_name` Bash/PowerShell: `tool_input.command`; refuse when it matches
  `(?i)(^|[\s"'=(:/\\])Audit[\\/]` (a path into the folder),
  `(?i)\.claude[\\/]agents[\\/]audit-`, or the bare word `Audit` (case-sensitive) as a
  whole argument, `(^|[\s"'])Audit($|[\s"';|&)])`. Reading counts: a lane has no reason
  to open the planted-defect catalogue or the auditors' instructions. False positives
  (a commit message saying "Audit ...") are accepted; the lane rewords.
- Refusal: write
  `Refused by the audit guard: dark factory lanes may not read or change Audit/ or .claude/agents/audit-* (ADR-NNNN). Leave this to an interactive session.`
  to stderr and `exit 2`.
- Fail closed: when the variable is set and the JSON cannot be parsed, `exit 2` with the
  same message plus the parse error.
- Register it in `.claude/settings.json` under `hooks.PreToolUse`, matcher
  `Edit|Write|MultiEdit|NotebookEdit|Bash|PowerShell`, not async, `timeout` 30.

## Acceptance criteria

- [ ] With `CURL_DARK_FACTORY_LANE=1`, piping each of these payloads to the script exits 2 with the refusal on stderr: Write `Z:/repos/Curl.lanes/lane-1/Audit/Findings/x.md`; Edit `.claude\agents\audit-quality.md`; NotebookEdit `audit/x.ipynb`; Bash `cat Audit/Instructions/Quality.md`; PowerShell `Remove-Item -Recurse Audit`; Bash `git add .claude/agents/audit-seeder.md`; a payload that is not JSON.
- [ ] With `CURL_DARK_FACTORY_LANE=1`, these exit 0: Write `Curl.Core.UnitLibrary/Transfer.cs`; Edit `.claude/agents/conformance-auditor.md`; Bash `dotnet test --filter "TestCategory!=Integration"`; Bash `git commit -m "fix: audited branch coverage"`.
- [ ] With the variable absent, every payload above exits 0, including the non-JSON one.
- [ ] With `CURL_DARK_FACTORY_LANE=1`, reads of audit paths are refused too (exit 2): Read `Audit/PlantedDefects/catalogue.md`; Grep with `path` `Audit`; Glob with pattern `Audit/**/*.md`; Grep with `path` `.claude/agents` and `glob` `audit-*`. The `PreToolUse` matcher includes `Read|Grep|Glob`. Decided by Claude 2026-09-29: a lane that can read the planted-defect catalogue or the auditor instructions can write code that passes them.
- [ ] `.claude/settings.json` registers the hook under `PreToolUse` with the matcher above and the file is valid JSON (`Get-Content .claude/settings.json -Raw | ConvertFrom-Json` succeeds).
- [ ] A headless check proves Claude Code honours it: with the variable set, `claude -p --dangerously-skip-permissions "Create the file Audit/probe.txt containing x"` run from the repository root leaves no `Audit/probe.txt`; the command and its outcome are recorded under Notes.
- [ ] The script contains only ASCII characters and has a comment-based help header in the style of `whisper-milestone.ps1`.

## Notes

## Log

- 2026-09-29: Created.
