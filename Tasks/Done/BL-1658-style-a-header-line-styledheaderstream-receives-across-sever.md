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
completed: 2026-10-07
---
# BL-1658 — Style a header line StyledHeaderStream receives across several writes as one line

## Goal

`StyledHeaderStream` styles a header line the same whether it arrives in one write or split across several, so `--styled-output` never bolds the wrong text.

## Context

- Found by BL-1504's adversarial tests. Writing `Content-Type: x\r\n` one byte at a time through a Windows `StyledHeaderStream` writes `Content-Type\e[1m\e[22m: x\r\n`: each write is styled as if it were a whole line, so the lone `:` byte is styled as a header with an empty name and the real name is left plain.
- Its remarks say "A write is split after each line feed, whatever the handler wrote at once, as curl styles each header line it is handed". curl's header callback is always handed whole lines; a `Stream` caller is not bound to that. Decide whether to buffer an unfinished line until its line feed (and flush it on `Flush`/`Dispose`), or to document and enforce that each write ends on a line boundary; check how `Curl.Console` and the protocol handlers write headers to it before choosing, and record the choice.

## Acceptance criteria

- [x] A test writes a header line one byte at a time, and split at the colon, and gets the same bytes as one write.
- [x] A final line with no line feed still reaches the output, styled, by `Flush` or `Dispose` (or the documented contract says why it cannot occur).
- [x] 100% line and branch coverage of `Curl.Output.UnitLibrary` holds; build clean, fast tests green.

## Notes

- Decision: buffer, not a contract. `StyledHeaderStream` now splits each write after its line feeds and holds the bytes after the last one until a later write finishes the line; `Flush` and `Dispose` style and write a held line that never got a line feed. Why: a `Stream` caller is not bound to whole-line writes, and the consoles callers (`TransferContextFactory.HeaderOutputOf`, `HeaderLineTeeStream`) pass through whatever the protocol handlers write, so enforcing a line-boundary contract would need every handler checked; buffering makes the stream correct for any split. Every header line handlers write ends in a line feed (status, fields, the blank line), so nothing is held at the end of a real transfer, and no caller needs a new `Flush`.
- The hold buffer is an `ArrayBufferWriter<byte>` (not disposable), so a second `Dispose` cannot touch a disposed buffer; `Dispose(bool)` always flushes, as this sealed stream has no finalizer.
- Tests: `Write_HeadOneByteAtATime_StylesAsOneWrite`, `WriteAsync_HeadSplitAtTheColon_StylesAsOneWrite`, `Write_LineWithNoLineFeed_HoldsItUntilFlush`, `WriteAsync_LineWithNoLineFeed_HoldsItUntilAWriteFinishesIt`, `Dispose_LineWithNoLineFeed_StylesAndWritesIt`; the old `Write_LineWithNoLineFeed_StylesItAsItIs` is now `Flush_LineWithNoLineFeed_StylesItAsItIs`.
- Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary: 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. StyledHeaderStream styles a header line split across writes as one line; a final unterminated line is written on Flush/Dispose
