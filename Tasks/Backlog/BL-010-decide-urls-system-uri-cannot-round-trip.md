---
id: BL-010
title: Decide how to represent URLs System.Uri cannot round-trip
priority: Normal
assignee: Stewart
pipeline: docs
depends-on: [BL-007]
requirement: none
created: 2026-09-25
completed:
---
# BL-010 — Decide how to represent URLs `System.Uri` cannot round-trip

## Goal

There is an agreed way to represent URLs that `System.Uri` alters, recorded as an
Architecture Decision Record.

## Context

The cases are `file:///C:%2FWindows/win.ini`, `file://user:pass@localhost/x`, `c|`
drive letters, `file:////server/share` folding to Universal Naming Convention paths,
and a literal `..`. See the "Known limitation" section of
`Documentation/Planning/Decisions/ADR-0003-itransfercontext-carries-transfer-options.md`,
measured against curl 8.21.0. This is owned jointly by Core and HTTP, because it also
affects `--path-as-is`. The options are to replace `System.Uri`, wrap it, or pre-parse
ahead of it.

## Acceptance criteria

- [ ] The decision covers the spellings `System.Uri` **refuses** as well as those it
      silently alters. Measured against curl 8.21.0 on 2026-09-25: `file://C:` gives
      exit 37 `Could not open file C:`, and `file://ab:/x` gives exit 3 - but
      `new Uri(...)` throws `UriFormatException` ("A Dos path must be rooted") for both,
      so neither can reach a protocol handler while `ITransferContext.Url` is a `Uri`.
      `Curl.Protocol.File.UnitTests` has had to skip `file://C:` for this reason.

- [ ] Stewart has chosen between replacing, wrapping, or pre-parsing ahead of
      `System.Uri`.
- [ ] An ADR under `Documentation/Planning/Decisions/` records the choice, each case
      above, and how it is represented.

## Notes

Claude can draft a proposed ADR comparing the three options. If that would help,
file it as a separate task assigned to Claude and add it to `depends-on`.

## Log

- 2026-09-25: Migrated from Documentation/Planning/Backlog.md (Ready). Assigned to Stewart because the decision is his.
- 2026-09-25: Scope widened to the two spellings System.Uri throws on, not only those it alters (conformance re-audit).
