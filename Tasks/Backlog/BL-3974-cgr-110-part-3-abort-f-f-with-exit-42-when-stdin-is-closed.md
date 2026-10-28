---
id: BL-3974
title: CGR-110 part 3: abort -F f=@- with exit 42 when stdin is closed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3570, BL-3893, BL-3972, BL-3973, BL-3985]
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

- 2026-10-28 (lane 1): `Curl.Engine` must never reference `Curl.Cli` (Curl.Engine.UnitLibrary/CLAUDE.md), so `MimePostForm` cannot see `Curl.Cli.ClosedStandardInputStream`. Decided: move the type to `Curl.Core` (which Engine, Cli and Console all reference); that move is in this run's stash and also in BL-3985's scope. Unit work done and built green in the stash: `MimePostForm.StandardInputReadOf` returns `ReadCallbackResult.Abort` for a `ClosedStandardInputStream`, tests in `Curl.Console.UnitTests/Cgr110Tests.cs` (abort when closed, 0 when empty). On resume, take MimePostForm.cs and Cgr110Tests.cs from the stash, and the move only if BL-3985 has not landed it; write the ADR (number it then; the doc comment cites ADR-0956 as a placeholder).
- Real curl measured: `Record-CurlExchange.ps1 -Port 18974 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello' -Curl "C:\Program Files\Git\bin\bash.exe" -CurlArgs '-c','curl -sS -F f=@- http://127.0.0.1:18974/ <&-; echo exit=$?'`: exit 42, stderr `curl: (42) operation aborted by callback`, stdout empty (the response is NOT printed, unlike this task's Goal says), request head with `Transfer-Encoding: chunked` and `Expect: 100-continue`, then one 0x6c chunk holding the part's head.
- Curl measured the same way (`dotnet run --project Curl.Console --no-build -- ...`): exit 0, `hello` printed, `Content-Length: 162`, empty part. The tool's `-F` goes through `Curl.Core`'s `MultipartFormBodyBuilder.AddStandardInputPartAsync`, which reads stdin up front and never reaches `MimePostForm`'s callback, so the end-to-end criterion needs that path changed: filed BL-3985 (outside this task's touches) and requeued on it.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 32acf4e5 20956cfc c259902e.
- 2026-10-28: Doing -> Backlog. Waiting on BL-3985: the tool's -F @- path (Curl.Core MultipartFormBodyBuilder) reads stdin up front, so the end-to-end exit 42 needs it; unit fix is in the stash
