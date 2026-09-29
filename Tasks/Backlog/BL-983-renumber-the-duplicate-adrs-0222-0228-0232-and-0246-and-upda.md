---
id: BL-983
title: Renumber the duplicate ADRs 0222, 0228, 0232 and 0246 and update every reference
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed:
---
# BL-983 — Renumber the duplicate ADRs 0222, 0228, 0232 and 0246 and update every reference

## Goal

ADR numbers 0222, 0228, 0232 and 0246 each name exactly one record in `Documentation/Planning/Decisions`: in each pair the record fewer references cite gets the next free number, its title line and file name say so, `README.md` indexes both in number order above `## Template`, and every reference points at the record it meant.

## Context

- Parallel lanes numbered ADRs from their own copies of the folder (found during BL-887, 2026-09-29). The pairs:
  - `ADR-0222-curl-s-own-diagnostic-log-is-log-level-and-log-file-off-by-default-with-zero-extra-bytes.md` and `ADR-0222-the-hand-built-tcp-client-sends-the-platform-profile-hello-cut-to-what-it-can-honour.md`
  - `ADR-0228-a-websocket-upgrade-answers-no-401-as-curl-8-21-0-sends-it-once.md` and `ADR-0228-the-runner-opens-the-diagnostic-log-once-the-command-line-is-accepted-and-writes-it-holding-the-write-gate.md`
  - `ADR-0232-an-empty-authorization-value-sends-the-request-again-without-one.md` and `ADR-0232-the-hand-built-kerberos-has-des3-cbc-sha1-as-mit-1-22-does.md`
  - `ADR-0246-a-kept-digest-answer-is-counted-on-from-the-value-as-sent.md` and `ADR-0246-styled-output-bolds-header-names-and-links-location-on-a-terminal-as-curl-does.md`
- BL-663 owns 0086, 0088, 0093 and 0109; BL-886 owns 0187 and 0193; BL-887 renumbered 0158, 0165 and 0200 (to 0254, 0255, 0256).
- `README.md` has the 0228 (runner) and 0248 rows below `## Template` rather than in the index; move them into the index too.
- Find references with `git grep -n "ADR-0222\b"` (and the others) outside `Tasks/Done/<timestamp>/`; read each to tell which record it means. Fill `touches` with the project folders that cite them before starting. Code changes are comments only.

## Acceptance criteria

- [ ] `Get-ChildItem Documentation/Planning/Decisions -Filter 'ADR-02[24]*.md'` and `-Filter 'ADR-023*.md'` show 0222, 0228, 0232 and 0246 once each.
- [ ] `Documentation/Planning/Decisions/README.md` has one row per record of the four pairs, and the 0248 row, in number order in the index above `## Template`.
- [ ] A search for each renumbered record's old number finds only references to the record that kept it; each renumbered record's references use its new number.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-09-29: Created.
