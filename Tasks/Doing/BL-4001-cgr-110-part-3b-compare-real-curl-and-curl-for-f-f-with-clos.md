---
id: BL-4001
title: CGR-110 part 3b: compare real curl and Curl for -F f=@- with closed stdin
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3570, BL-3893, BL-3972, BL-3973, BL-3985, BL-4000]
touches: [Curl.Console.UnitTests/Cgr110Tests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4001 — CGR-110 part 3b: compare real curl and Curl for -F f=@- with closed stdin

## Goal

Real curl and Curl are shown to give the same exit code, stdout and request head bytes for `curl -sS -F f=@- URL <&-` against a loopback server.

## Context

Split from BL-3974. BL-4000 makes the library abort; BL-3985 made the tool's `-F` path stream stdin. This task verifies the end-to-end result.

## Acceptance criteria

- [ ] Real curl and Curl are compared against a loopback server for `curl -sS -F f=@- URL <&-` (exit 42, stderr `curl: (42) operation aborted by callback`, request head bytes on the wire, stdout empty as real curl measured); commands and results are recorded in Notes.
- [ ] Any difference found is fixed with a focused test in `Curl.Console.UnitTests/Cgr110Tests.cs` that fails before and passes after (ADR-0482), or filed as a new task when the fix lies outside this task's touches.
- [ ] dotnet build is clean and dotnet test --filter "TestCategory!=Integration" passes.

## Notes

- Split from BL-3974. BL-3974's Goal says the response is printed; real curl measured otherwise (stdout empty), so compare against what real curl does.
- Measure with `Record-CurlExchange.ps1 -Port 18974 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello' -Curl "C:\Program Files\Git\bin\bash.exe" -CurlArgs '-c','curl -sS -F f=@- http://127.0.0.1:18974/ <&-; echo exit=$?'`, and Curl the same way with `dotnet run --project Curl.Console --no-build -- ...`.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 1bac223e e5faf1f9 c259902e.
