---
id: BL-010
title: Decide how to represent URLs System.Uri cannot round-trip
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-007, BL-128]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-25
completed: 2026-09-26
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

- [x] The decision covers the spellings `System.Uri` **refuses** as well as those it
      silently alters. Measured against curl 8.21.0 on 2026-09-25: `file://C:` gives
      exit 37 `Could not open file C:`, and `file://ab:/x` gives exit 3 - but on
      .NET 10.0.12 `new Uri(...)` throws `UriFormatException` for both: "A Dos path
      must be rooted" for `file://C:` and "The hostname could not be parsed" for
      `file://ab:/x`, so neither can reach a protocol handler while
      `ITransferContext.Url` is a `Uri`.
      `Curl.Protocol.File.UnitTests` has had to skip `file://C:` for this reason.

- [x] Claude has chosen, under Stewart's delegation, between replacing, wrapping, or pre-parsing ahead of
      `System.Uri`.
- [x] An ADR under `Documentation/Planning/Decisions/` records the choice, each case
      above, and how it is represented.

## Notes

**Stewart, 2026-09-26:** delegated the decision to Claude. Take BL-128's recommendation unless the proposed ADR shows a reason not to, turn that ADR from Proposed to Accepted (marked "Decided by Claude under Stewart's delegation"), and file the implementation tasks.

Claude can draft a proposed ADR comparing the three options. If that would help,
file it as a separate task assigned to Claude and add it to `depends-on`.

**Claude, 2026-09-26 (dark factory lane 1):**

- Decision: option 1, **replace** `System.Uri` with a curl-compatible `CurlUrl` in
  `Curl.Protocol.Abstractions.UnitLibrary`. ADR-0010 moved from Proposed to Accepted,
  marked "Decided by Claude under Stewart's delegation"; its Decision section now
  states the choice, why, and the implementation tasks, and a new addendum records what
  changed in the code between BL-128's draft and this decision (HTTP, cookies,
  redirects and proxy selection now read `Uri`; `--path-as-is` is parsed but not
  carried). The case table, which already covers the two refused spellings
  (`file://C:`, `file://ab:/x`) and `file:///C:`, is unchanged and says how each case
  is represented under "Replace".
- Why not wrap, the cheaper fallback: it leaves `--path-as-is` and `%2F` wrong for
  HTTP, and a curl-compatible parser is needed anyway by BL-030 and BL-186.
- Filed: BL-292 (`CurlUrl` and parser), BL-293 (`ITransferContext.PathAsIs`), BL-294
  (switch the contract to `CurlUrl`, runs alone), BL-295 (open the refused `file://`
  spellings, unskip `file://C:`). BL-030 now also depends on BL-292; BL-186 on BL-293
  and BL-294.
- The task had no `touches`; added `Documentation/Planning/Decisions`, the only
  non-board folder it changed.
- ADR-0003's "Known limitation" section is left as written: ADR-0003 is Accepted and
  so immutable, and ADR-0010 now answers it.

## Log

- 2026-09-25: Migrated from Documentation/Planning/Backlog.md (Ready). Assigned to Stewart because the decision is his.
- 2026-09-25: Scope widened to the two spellings System.Uri throws on, not only those it alters (conformance re-audit).
- 2026-09-26: Waits on BL-128, a proposed ADR Stewart asked for before deciding.
- 2026-09-26: Stewart delegated the decision to Claude; reassigned. Still waits on BL-128.
- 2026-09-26: First acceptance criterion corrected: "A Dos path must be rooted" is thrown for `file://C:` only; `file://ab:/x` throws "The hostname could not be parsed" (BL-135).
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. ADR-0010 accepted: CurlUrl replaces System.Uri for transfer URLs; implementation filed as BL-292 to BL-295
