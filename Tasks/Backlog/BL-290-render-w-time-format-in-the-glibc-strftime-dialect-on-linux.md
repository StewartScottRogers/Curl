---
id: BL-290
title: Render -w %time{format} in the glibc strftime dialect on Linux and macOS
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-279]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-290 — Render -w %time{format} in the glibc strftime dialect on Linux and macOS

## Goal

On Linux and macOS, `-w "%time{format}"` prints what curl 8.21.0's OpenSSL build prints there, through glibc's `strftime` in the C locale.

## Context

- BL-279 made `WriteOutTimeFormatter` follow the Windows C runtime (ADR-0038). glibc knows more conversions (`%C %D %e %F %g %G %h %k %l %n %P %r %R %t %T %u %V`, the `_ - 0 ^ #` flags, widths, the `E` and `O` modifiers), and its C-locale `%c`, `%x`, `%X` are `Sun Sep 27 03:30:08 2026`, `09/27/26`, `03:30:08`.
- curl substitutes `%f`, `%z` and `%Z` itself on every platform (`src/tool_writeout.c`, `outtime`).
- Measure on a Linux curl 8.21.0 before pinning; the renderer needs a way to choose the dialect, as it already takes `writesLineFeedAsCrLf` for the Windows line ends.

## Acceptance criteria

- [ ] `WriteOutTimeFormatter` (or a sibling) renders the glibc conversions and flags measured on Linux curl 8.21.0, pinned in tests with a fake `TimeProvider`, and the Windows dialect's tests still pass.
- [ ] `WriteOutTemplateRenderer` chooses the dialect from a constructor argument, documented in ADR-0038 or a successor ADR.
- [ ] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Output.UnitLibrary`.

## Notes

## Log

- 2026-09-26: Created.
