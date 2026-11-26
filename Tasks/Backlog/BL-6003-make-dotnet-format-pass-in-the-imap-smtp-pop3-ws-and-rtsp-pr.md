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
completed:
---
# BL-6003 — Make dotnet format pass in the IMAP, SMTP, POP3, WS and RTSP protocol tests

## Goal

`dotnet format Curl.slnx --verify-no-changes --include <each path in touches>` exits 0: the `using` order, whitespace and final newlines in this task's touches follow `.editorconfig`.

## Context

Part of BL-5923, split by project group so lanes run the parts in parallel. On 2026-11-26 `dotnet format` reported 680 files across 53 projects: 636 IMPORTS (`using` order), 31 FINALNEWLINE and 13 WHITESPACE. Line endings are left to git (ADR-1417), so there are no ENDOFLINE fixes. Run `dotnet format Curl.slnx --include <each path in touches>`, and change nothing outside touches. It is a formatting-only change; if `dotnet format` wants to change code beyond `using` order and whitespace, leave that file alone and say why under Notes.

## Acceptance criteria

- [ ] `dotnet format Curl.slnx --verify-no-changes --include <each path in touches>` exits 0.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
