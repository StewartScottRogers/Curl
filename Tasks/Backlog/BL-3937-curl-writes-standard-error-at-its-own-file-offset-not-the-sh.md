---
id: BL-3937
title: Curl writes standard error at its own file offset, not the shared one, when 2>&1 sends it to a file on Linux
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3936]
touches: [Curl.Console/StandardOutputOpener.cs, Curl.Console.UnitTests/StandardOutputOpenerTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3937 — Curl writes standard error at its own file offset, not the shared one, when 2>&1 sends it to a file on Linux

## Goal

On Linux, Curl's standard error line written to a file shared with the shell (`{ echo a; curl ...; echo b; } > f 2>&1`) lands between the shell's lines, as curl's does.

## Context

Found by BL-3824 on 2026-10-28 in WSL (Ubuntu): `{ echo before...; curl -sS http://127.0.0.1:21199/; echo after; } > /tmp/w 2>&1; cat /tmp/w` gives `before`, the `curl: (7) ...` line, then `after` with curl 8.18.0; with Curl the line is written at the wrong offset (it came out after `after`, its start overwritten). `StandardOutputOpener.OpenHandle` opens descriptors 1 and 2 as an unbuffered `FileStream`, which for a seekable file keeps its own position and writes with `pwrite`, rather than `write` at the descriptor's shared offset. Standard output (`> f` shared with the shell) likely has the same defect. Start in `Curl.Console/StandardOutputOpener.cs`: write through a stream that uses the descriptor's own offset (for example the runtime's `SystemNative_Write` shim, which `write`s at the shared offset).

## Acceptance criteria

- [ ] On Linux, the command above gives the same file bytes (apart from the timing in the message) through Curl as through real curl, with the commands and both results recorded in Notes.
- [ ] A focused unit test in `Curl.Console.UnitTests` fails before the fix and passes after it (ADR-0482).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
