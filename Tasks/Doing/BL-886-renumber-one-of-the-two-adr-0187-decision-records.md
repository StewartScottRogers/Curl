---
id: BL-886
title: Renumber one of the two ADR-0187 decision records
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed:
---
# BL-886 — Renumber one of the two ADR-0187 decision records

## Goal

Every ADR number in `Documentation/Planning/Decisions` names exactly one decision: the duplicate ADR-0187 (and the duplicate ADR-0193) get the next free numbers, and every reference to the renumbered one is updated.

## Context

- Parallel lanes numbered ADRs from their own copies of the folder, so two numbers are taken twice (seen 2026-09-29):
  - `ADR-0187-a-forward-proxy-s-407-is-answered-once-like-a-401.md` (BL-603) and `ADR-0187-http-3-stream-resets-follow-curl-8-21-0-and-a-refused-stream-is-retried-on-a-new-connection.md` (BL-839, used by BL-834's code comments and tests).
  - `ADR-0193-a-redirect-hop-sends-its-own-url-s-user-information-unless-command-line-credentials-win.md` and `ADR-0193-pinnedpubkey-is-checked-in-the-shared-certificate-judgement-on-every-platform.md`.
- Keep the number on the record whose number more code already cites (grep `ADR-0187` and `ADR-0193` across the repository, `.cs` files included); renumber the other and update its references, including task files outside the `Done` archives.

## Acceptance criteria

- [ ] `Get-ChildItem Documentation/Planning/Decisions -Filter 'ADR-*.md'` shows no number twice.
- [ ] A grep for each renumbered record's old number finds only references to the record that kept it.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean if any `.cs` file changed.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
