---
id: BL-128
title: Draft a proposed ADR comparing ways to represent URLs System.Uri cannot round-trip
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed:
---
# BL-128 — Draft a proposed ADR comparing ways to represent URLs System.Uri cannot round-trip

## Goal

A proposed ADR (status Proposed) in `Documentation/Planning/Decisions/` compares replacing, wrapping and pre-parsing ahead of `System.Uri`, so Stewart can make the BL-010 decision from it.

## Context

Stewart asked for this before deciding BL-010 (2026-09-26). The cases, all measured against curl 8.21.0, are listed in BL-010 and in the "Known limitation" section of `Documentation/Planning/Decisions/ADR-0003-itransfercontext-carries-transfer-options.md`: `file:///C:%2FWindows/win.ini`, `file://user:pass@localhost/x`, `c|` drive letters, `file:////server/share`, a literal `..` (with `--path-as-is`), and the two spellings `System.Uri` refuses outright, `file://C:` (curl: exit 37) and `file://ab:/x` (curl: exit 3). `ITransferContext.Url` is a `Uri` today, so the choice reaches every protocol library.

## Acceptance criteria

- [ ] A new ADR, status Proposed and the next free number, describes each of the three options: what type `ITransferContext.Url` becomes, which projects change, and roughly how much work it is.
- [ ] For every case above, the ADR states what each option does with it and whether that matches curl 8.21.0.
- [ ] The ADR ends with a recommendation and why, and leaves the decision to Stewart.
- [ ] `Documentation/Planning/Decisions/README.md` lists the new ADR as Proposed.
- [ ] No production code changes.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
