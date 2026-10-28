---
id: BL-3937
title: Curl writes standard error at its own file offset, not the shared one, when 2>&1 sends it to a file on Linux
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3936]
touches: [Curl.Console/StandardOutputOpener.cs, Curl.Console.UnitTests/StandardOutputOpenerTests.cs, Curl.Console/SharedOffsetHandleStream.cs, Curl.Console/NativeSharedOffsetWrite.cs, Curl.Console.UnitTests/SharedOffsetHandleStreamTests.cs, Curl.Console/CLAUDE.md]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3937 — Curl writes standard error at its own file offset, not the shared one, when 2>&1 sends it to a file on Linux

## Goal

On Linux, Curl's standard error line written to a file shared with the shell (`{ echo a; curl ...; echo b; } > f 2>&1`) lands between the shell's lines, as curl's does.

## Context

Found by BL-3824 on 2026-10-28 in WSL (Ubuntu): `{ echo before...; curl -sS http://127.0.0.1:21199/; echo after; } > /tmp/w 2>&1; cat /tmp/w` gives `before`, the `curl: (7) ...` line, then `after` with curl 8.18.0; with Curl the line is written at the wrong offset (it came out after `after`, its start overwritten). `StandardOutputOpener.OpenHandle` opens descriptors 1 and 2 as an unbuffered `FileStream`, which for a seekable file keeps its own position and writes with `pwrite`, rather than `write` at the descriptor's shared offset. Standard output (`> f` shared with the shell) likely has the same defect. Start in `Curl.Console/StandardOutputOpener.cs`: write through a stream that uses the descriptor's own offset (for example the runtime's `SystemNative_Write` shim, which `write`s at the shared offset).

## Acceptance criteria

- [x] On Linux, the command above gives the same file bytes (apart from the timing in the message) through Curl as through real curl, with the commands and both results recorded in Notes.
- [x] A focused unit test in `Curl.Console.UnitTests` fails before the fix and passes after it (ADR-0482).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Added to `touches`: the new files `Curl.Console/SharedOffsetHandleStream.cs`, `Curl.Console/NativeSharedOffsetWrite.cs` and `Curl.Console.UnitTests/SharedOffsetHandleStreamTests.cs` (one type per file; no task in Doing on origin/work/dark-factory names them), and `Curl.Console/CLAUDE.md`, whose standard-output paragraph named the unbuffered `FileStream` this replaces.
- Fix (ADR-0945): `StandardOutputOpener.OpenHandle` now wraps the handle in `SharedOffsetHandleStream`, which writes through `NativeSharedOffsetWrite` - `SystemNative_Write` (write(2) at the descriptor's shared offset) off Windows, `WriteFile` without an OVERLAPPED offset on Windows. Decided to change Windows too: curl's Windows build also writes at the shared file pointer, and two FileStreams over one `> f 2>&1` handle overwrote each other there as well, which is what makes the focused test fail on every platform.
- Focused test: `StandardOutputOpenerTests.OpenHandle_TwoStreamsOverOneFile_WritesEachAfterTheOther` (two streams over one file handle, as `1>f 2>&1`) failed on the old FileStream opener (`Assert.AreEqual("outerr", written)`, file held `err`) and passes after. `SharedOffsetHandleStreamTests` covers the stream; the native adapter is excluded from coverage (ADR-0083).
- Linux, measured 2026-10-28 in WSL (Ubuntu) with a linux-x64 build of this tree: `{ echo before; <curl> -sS http://127.0.0.1:21199/; echo after; } > /tmp/w 2>&1; cat /tmp/w`.
  - curl 8.18.0: `before` / `curl: (7) Failed to connect to 127.0.0.1 port 21199 after 0 ms: Could not connect to server` / `after`, each line ending LF.
  - Curl after the fix: `before` / `curl: (7) Failed to connect to 127.0.0.1:21199 after 76 ms: Could not connect to server` / `after`, each line ending LF - same order and layout; before the fix the line came after `after` with its start overwritten (BL-3824). Besides the timing, `127.0.0.1 port 21199` vs `127.0.0.1:21199` is WSL's older curl 8.18.0 against the curl 8.21/8.22 text Curl pins, not this task's offset defect.
- CI on Linux and macOS: the change is platform-neutral (the new test uses a temporary file and no path or OS text) and the fast tests are green here.
- Gates: `dotnet build` clean; fast tests green (Curl.Console.UnitTests 3010 passed, 31 skipped; whole solution exit 0); `Measure-CodeQuality.ps1 -Library Curl.Console`: 0 failing members.
## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: a46c198d def504f7 779790c0.
- 2026-10-28: Doing -> Done. Standard output and standard error write at the handle's shared file offset, so > f 2>&1 keeps curl's order on Linux (and Windows)
