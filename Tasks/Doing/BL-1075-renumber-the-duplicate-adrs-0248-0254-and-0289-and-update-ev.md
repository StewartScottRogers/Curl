---
id: BL-1075
title: Renumber the duplicate ADRs 0248, 0254 and 0289 and update every reference
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-10-01
completed:
---
# BL-1075 — Renumber the duplicate ADRs 0248, 0254 and 0289 and update every reference

## Goal

ADR numbers 0248, 0254 and 0289 each name exactly one record in `Documentation/Planning/Decisions`: in each pair the record fewer references cite gets the next free number, its title line and file name say so, `README.md` indexes every record in number order above `## Template`, and every reference points at the record it meant.

## Context

- Parallel lanes numbered ADRs from their own copies of the folder; found during BL-983 (2026-10-01), which renumbered 0222, 0228, 0232 and 0246 to 0235-0238 the same way. The pairs:
  - `ADR-0248-a-negotiate-token-in-a-2xx-is-not-checked-and-the-kept-context-is-ended.md` and `ADR-0248-the-console-finds-the-ssh-known-hosts-file-as-curls-tool-does.md` (BL-576)
  - `ADR-0254-tcpconnector-races-address-families-after-the-happy-eyeballs-timeout.md` and `ADR-0254-tls-1-2-and-below-run-over-a-byte-stream-in-their-own-connection-and-stream.md`
  - `ADR-0289-an-ssh-connection-reset-after-the-key-exchange-fails-as-libssh2-reports-each-step.md` and `ADR-0289-http-3-through-an-http-proxy-runs-quic-in-a-connect-udp-tunnel.md`
- `README.md` has rows for 0289 (SSH reset), 0290 and 0291 below `## Template`; move them into the index. The 0248 SSH known-hosts and 0254 happy-eyeballs records have no row; add them.
- Find references with `git grep -n "ADR-0248\b"` (and the others) outside `Tasks/Done/<timestamp>/`; read each to tell which record it means. Before starting, widen `touches` to every project folder and task file that cites a renumbered record. Code changes are comments only. Pick the lowest numbers no file, branch or task cites (check `origin/work/dark-factory`).

## Acceptance criteria

- [ ] `Get-ChildItem Documentation/Planning/Decisions -Filter 'ADR-0248*.md'`, `'ADR-0254*.md'` and `'ADR-0289*.md'` each show one file.
- [ ] `Documentation/Planning/Decisions/README.md` has one row per record of the three pairs, and the 0290 and 0291 rows, in number order in the index above `## Template`, with nothing below `## Template` but the template.
- [ ] A search for each renumbered record's old number outside `Tasks/Done/<timestamp>/` finds only references to the record that kept it; each renumbered record's references use its new number.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
