---
id: BL-142
title: Record TransferResult.TimeConditionUnmet in FR-009 and ADR-0003
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-136]
touches: [Documentation/Product/Requirements.md, Documentation/Planning/Decisions/ADR-0003-itransfercontext-carries-transfer-options.md]
requirement: FR-009
created: 2026-09-26
completed: 2026-09-26
---
# BL-142 — Record TransferResult.TimeConditionUnmet in FR-009 and ADR-0003

## Goal

FR-009 and ADR-0003 state that an unmet `-z`/`--time-cond` returns
`TransferResult.TimeConditionNotMet`, and that `Curl.Console` then creates no `-o` file and
leaves an existing one's content untouched.

## Context

- BL-136 added `TransferResult.TimeConditionUnmet` (init-only, default `false`) and the
  `TransferResult.TimeConditionNotMet(DateTimeOffset?)` factory
  (`Curl.Protocol.Abstractions.UnitLibrary/TransferResult.cs`); `FileProtocolHandler`
  returns it for an unmet condition and `DeferredOutputFileStream.CompleteAsync` skips the
  empty-file create for it. Those files were in BL-136's `touches`; the documents were not.
- FR-009 (`Documentation/Product/Requirements.md`) says "an unmet condition is a success
  with no body, exit 0" and says nothing about the `-o` file.
- ADR-0003 governs the result/context shape and takes a dated amendment line whenever the
  record gains a member (see its BL-019 and BL-020 amendments).
- Measured on curl 8.21.0 (Windows, 2026-09-26): `curl -z "1 Jan 2030" -o out.txt file:///...`
  exits 0 and creates no `out.txt`; an existing `out.txt` keeps its content.

## Acceptance criteria

- [x] FR-009 states that an unmet condition creates no `-o` file and leaves an existing
      one's content untouched (BL-136).
- [x] ADR-0003 has a dated amendment naming `TransferResult.TimeConditionUnmet` and
      `TransferResult.TimeConditionNotMet`, and why a flag on the result was chosen over
      a new exit code (the transfer is a success; exit 0).

## Notes

- Done directly rather than through `align-and-document`: two paragraphs in two documents.
- FR-009 now names the `-o` behaviour and the `TimeConditionNotMet`/`TimeConditionUnmet`
  mechanism; ADR-0003 has a dated amendment (marked "Decided by Claude under Stewart's
  delegation") giving why a flag on a successful result beats a new exit code, and why an
  init-only property beats a fifth positional member.
- Found: FR-011 says `-R` applies on an unmet `-z`, but `FileProtocolHandler` returns
  `TimeConditionNotMet()` with no timestamp. Outside this task's scope (code); filed as
  BL-274.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. FR-009 and ADR-0003 record TransferResult.TimeConditionUnmet and the no -o file behaviour of an unmet -z
