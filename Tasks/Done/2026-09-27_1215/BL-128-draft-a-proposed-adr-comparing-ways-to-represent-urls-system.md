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
completed: 2026-09-26
---
# BL-128 — Draft a proposed ADR comparing ways to represent URLs System.Uri cannot round-trip

## Goal

A proposed ADR (status Proposed) in `Documentation/Planning/Decisions/` compares replacing, wrapping and pre-parsing ahead of `System.Uri`, so Stewart can make the BL-010 decision from it.

## Context

Stewart asked for this before deciding BL-010 (2026-09-26). The cases, all measured against curl 8.21.0, are listed in BL-010 and in the "Known limitation" section of `Documentation/Planning/Decisions/ADR-0003-itransfercontext-carries-transfer-options.md`: `file:///C:%2FWindows/win.ini`, `file://user:pass@localhost/x`, `c|` drive letters, `file:////server/share`, a literal `..` (with `--path-as-is`), and the two spellings `System.Uri` refuses outright, `file://C:` (curl: exit 37) and `file://ab:/x` (curl: exit 3). `ITransferContext.Url` is a `Uri` today, so the choice reaches every protocol library.

## Acceptance criteria

- [x] A new ADR, status Proposed and the next free number, describes each of the three options: what type `ITransferContext.Url` becomes, which projects change, and roughly how much work it is.
- [x] For every case above, the ADR states what each option does with it and whether that matches curl 8.21.0.
- [x] The ADR ends with a recommendation and why, and leaves the decision to Stewart.
- [x] `Documentation/Planning/Decisions/README.md` lists the new ADR as Proposed.
- [x] No production code changes.

## Notes

- Written by `align-and-document` as `ADR-0010-representing-urls-system-uri-cannot-round-trip.md`. The next free number was 0010 in this lane. If another lane takes 0010 first, renumber it when rebasing.
- Recommendation: Option 1, replace `System.Uri` with a curl-style URL type. It is the only option that matches curl 8.21.0 on every recorded case for every scheme. Wrap (Option 2) is the fallback. The decision stays with Stewart under BL-010.
- Where curl behaviour is not recorded in the repository, the ADR says *not recorded* instead of guessing (for example the messages for `file://user:pass@localhost/x` and `file://ab:/x`). No curl measurements were taken for this ADR. The `System.Uri` behaviour was probed on .NET 10.0.12.
- Three existing statements disagree with the code: the BL-010 exception message, the "scheme rewriting" remark in `ITransferContext.cs`, and FR-012's "no option parser yet". They are outside `touches`, so they are filed as BL-135 and not edited here.
- Only the ADR and the Decisions README changed. No `.cs` or project files were touched, so no build or test gate applies to this change.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Proposed ADR-0010 compares replacing, wrapping and pre-parsing ahead of System.Uri case by case against curl 8.21.0 and recommends replace, for Stewart to decide under BL-010
