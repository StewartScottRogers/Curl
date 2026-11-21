---
id: BL-5644
title: Make a POP3 custom request UIDL 2 end at its single-line reply instead of hanging
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Pop3.UnitLibrary/Pop3Command.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5644 — Make a POP3 custom request UIDL 2 end at its single-line reply instead of hanging

## Goal

A POP3 custom request with an argument whose reply is one line (`UIDL 2`, `LIST 2`) returns that line at once, as curl 8.22.0 does, instead of waiting for a multi-line terminator until the timeout.

## Context

From Surly cookbook bug report `2026-11-20T1836-Curl-bug-report.md` (batch 6 of the 1,000-recipe plan, and batch 5's CI on Linux and macOS), finding 5. Build tested: Curl `7d167fa10a` (`curl`, `curl-mcp`, `Curl.Api`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

A `Curl.Api` POP3 custom request `UIDL 2` hangs until the timeout. RFC 1939: `UIDL` and `LIST` with a message number answer one `+OK` line; without one they answer a multi-line listing ending in `.`. curl's pop3.c decides by the command (`LIST`/`UIDL` with an argument, plus its own `-X` rules); the port appears to always wait for the dot. Measure `curl -X "UIDL 2" pop3://...` and `curl -X "LIST 2"` with curl 8.22.0, and pin the output. Check the console as well as `Curl.Api`.

## Acceptance criteria

- [ ] `curl -X "UIDL 2" pop3://...` and `-X "LIST 2"` against a POP3 test server return at once with the same output and exit code as curl 8.22.0's recorded run.
- [ ] The same through `Curl.Api` returns at once, pinned by a test.
- [ ] `UIDL` and `LIST` with no argument still read the multi-line listing, pinned by the existing tests.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1836, finding 5.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 52effbf3 4ec6ae8a 80406a9b 3d836d5e.
