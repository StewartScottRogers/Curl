---
id: BL-002
title: Author the first functional requirements in Product/Requirements.md
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
requirement: none
created: 2026-09-25
completed: 2026-09-26
---
# BL-002 — Author the first functional requirements in `Product/Requirements.md`

## Goal

`Documentation/Product/Requirements.md` holds the first numbered functional
requirements, derived from the compatibility surface in the product overview.

## Context

The overview is done (BL-001), so requirements can now be derived from its
274-option compatibility surface. `docs-writer` owns the file: one behaviour per
requirement, each naming the upstream curl option it mirrors.

## Acceptance criteria

- [x] `Requirements.md` contains numbered requirements, one behaviour each, each
      naming the upstream curl option it mirrors and linking to curl.se.
- [x] Every requirement states the curl version it was checked against.

## Notes

Migrated as written. The original item did not say which options "the first"
requirements cover. Before this is run, `task-planner` should narrow it, for example
to the options Phase 1 needs, or split it by option group.

2026-09-26 (unattended run, default taken): scoped "the first" requirements to the
`file://` scheme and the curl options its handler honours, because BL-008 has already
delivered that handler, so each requirement can be checked against existing code and
upstream. Other Phase 1 option groups (HTTP, CLI, output) are left for later tasks.
Checked against curl 8.21.0, the version the product overview measures against.
Existing open file-scheme tasks (BL-011 to BL-027) are cited rather than duplicated.

## Log

- 2026-09-25: Migrated from Documentation/Planning/Backlog.md (Ready).
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Requirements.md holds FR-001 to FR-019 for the file:// scheme, each linked to curl.se and checked against curl 8.21.0
