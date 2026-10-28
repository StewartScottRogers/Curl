---
id: BL-3974
title: CGR-110 part 3: abort -F f=@- with exit 42 when stdin is closed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3570, BL-3893, BL-3972, BL-3973]
touches: [Curl.Engine.UnitLibrary/MimePostForm.cs, Curl.Console.UnitTests/Cgr110Tests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3974 — CGR-110 part 3: abort -F f=@- with exit 42 when stdin is closed

## Goal

curl -F f=@- with a closed stdin sends the request head, prints the server's response and ends with exit 42 'operation aborted by callback', as real curl does.

## Context

Split from BL-3639 (CGR-110, Documentation/Reviews/Curl-Gap-Report-2026-10-25.md: closed stdin with @- is read as empty and the request is sent; real curl 8.22.0 exits 26 for -d/--data-binary/--json and 42 for -F). Read BL-3639's Notes for the measurements and the decided plan.

## Acceptance criteria

- [ ] MimePostForm.WithStandardInput's read callback returns the read-abort result when the stream is a ClosedStandardInputStream; unit tests (Curl.Console.UnitTests/Cgr110Tests.cs) fail before and pass after (ADR-0482).
- [ ] Real curl and Curl are compared against a loopback server for 'curl -sS -F f=@- URL <&-' (exit 42, request head bytes on the wire, response printed); commands and results recorded in Notes.
- [ ] dotnet build is clean and dotnet test --filter "TestCategory!=Integration" passes.

## Notes

- Split from BL-3639.
- Needs ClosedStandardInputStream from BL-3972; end to end it also needs BL-3973's Program.cs swap, so verify the real-curl comparison once that has landed (if not yet, record the unit-level result and say so).

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 32acf4e5 20956cfc c259902e.
