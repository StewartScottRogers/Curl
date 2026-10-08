---
id: BL-1658
title: Style a header line StyledHeaderStream receives across several writes as one line
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1658 — Style a header line StyledHeaderStream receives across several writes as one line

## Goal

`StyledHeaderStream` styles a header line the same whether it arrives in one write or split across several, so `--styled-output` never bolds the wrong text.

## Context

- Found by BL-1504's adversarial tests. Writing `Content-Type: x\r\n` one byte at a time through a Windows `StyledHeaderStream` writes `Content-Type\e[1m\e[22m: x\r\n`: each write is styled as if it were a whole line, so the lone `:` byte is styled as a header with an empty name and the real name is left plain.
- Its remarks say "A write is split after each line feed, whatever the handler wrote at once, as curl styles each header line it is handed". curl's header callback is always handed whole lines; a `Stream` caller is not bound to that. Decide whether to buffer an unfinished line until its line feed (and flush it on `Flush`/`Dispose`), or to document and enforce that each write ends on a line boundary; check how `Curl.Console` and the protocol handlers write headers to it before choosing, and record the choice.

## Acceptance criteria

- [ ] A test writes a header line one byte at a time, and split at the colon, and gets the same bytes as one write.
- [ ] A final line with no line feed still reaches the output, styled, by `Flush` or `Dispose` (or the documented contract says why it cannot occur).
- [ ] 100% line and branch coverage of `Curl.Output.UnitLibrary` holds; build clean, fast tests green.

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
