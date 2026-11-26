---
id: BL-6003
title: Make dotnet format pass in the IMAP, SMTP, POP3, WS and RTSP protocol tests
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Imap.UnitTests, Curl.Original.Protocol.Smtp.UnitTests, Curl.Original.Protocol.Pop3.UnitTests, Curl.Original.Protocol.Ws.UnitTests, Curl.Original.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6003 — Make dotnet format pass in the IMAP, SMTP, POP3, WS and RTSP protocol tests

## Goal

`dotnet format Curl.slnx --verify-no-changes --include <each path in touches>` exits 0: the `using` order, whitespace and final newlines in this task's touches follow `.editorconfig`.

## Context

Part of BL-5923, split by project group so lanes run the parts in parallel. On 2026-11-26 `dotnet format` reported 680 files across 53 projects: 636 IMPORTS (`using` order), 31 FINALNEWLINE and 13 WHITESPACE. Line endings are left to git (ADR-1417), so there are no ENDOFLINE fixes. Run `dotnet format Curl.slnx --include <each path in touches>`, and change nothing outside touches. It is a formatting-only change; if `dotnet format` wants to change code beyond `using` order and whitespace, leave that file alone and say why under Notes.

## Acceptance criteria

- [x] `dotnet format Curl.slnx --verify-no-changes --include <each path in touches>` exits 0.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-11-26: `dotnet format` fixed 102 findings (101 IMPORTS, 1 FINALNEWLINE) in 101 files. The
  diff is `using` reorders and that one final newline only.
  `ImapProtocolHandlerNulByteTests.cs` shows as binary in git; `git diff --text` confirms it is a
  `using` reorder too.
- Give each `--include` folder a trailing slash. Without it the run matched no file and exited 0
  while the findings were still there; with it, `--verify-no-changes` exited 2 before the fix and
  0 after it.
- `dotnet build`: 0 warnings, 0 errors. Fast tests: exit 0, 115 test projects passed.
- No production code changed, so no quality measurement or ADR was needed.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 03521b86 3d836d5e.
- 2026-11-26: Doing -> Done. dotnet format verifies clean in the five protocol test projects; build clean and fast tests pass.
