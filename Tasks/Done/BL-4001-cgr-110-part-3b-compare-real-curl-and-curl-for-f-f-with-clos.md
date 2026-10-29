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
completed: 2026-10-28
---
# BL-4001 — CGR-110 part 3b: compare real curl and Curl for -F f=@- with closed stdin

## Goal

Real curl and Curl are shown to give the same exit code, stdout and request head bytes for `curl -sS -F f=@- URL <&-` against a loopback server.

## Context

Split from BL-3974. BL-4000 makes the library abort; BL-3985 made the tool's `-F` path stream stdin. This task verifies the end-to-end result.

## Acceptance criteria

- [x] Real curl and Curl are compared against a loopback server for `curl -sS -F f=@- URL <&-` (exit 42, stderr `curl: (42) operation aborted by callback`, request head bytes on the wire, stdout empty as real curl measured); commands and results are recorded in Notes.
- [x] Any difference found is fixed with a focused test in `Curl.Console.UnitTests/Cgr110Tests.cs` that fails before and passes after (ADR-0482), or filed as a new task when the fix lies outside this task's touches.
- [x] dotnet build is clean and dotnet test --filter "TestCategory!=Integration" passes.

## Notes

- Split from BL-3974. BL-3974's Goal says the response is printed; real curl measured otherwise (stdout empty), so compare against what real curl does.
- Measure with `Record-CurlExchange.ps1 -Port 18974 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello' -Curl "C:\Program Files\Git\bin\bash.exe" -CurlArgs '-c','curl -sS -F f=@- http://127.0.0.1:18974/ <&-; echo exit=$?'`, and Curl the same way with `dotnet run --project Curl.Console --no-build -- ...`.
- Measured 2026-10-28 with `-OutDirectory "$TEMP/bl4001/<real|ours>"`, real curl 8.22.0 (Git for Windows) and Curl run as `Curl.Console/bin/Debug/net10.0/curl.exe` in place of `curl` in the bash `-c` line. Both print only `exit=42` on stdout (curl's own stdout is empty), and stderr is `curl: (42) operation aborted by callback`.
- With the default `-RequestReadMilliseconds 1000`, real curl sent 336 bytes and Curl 222 (the head only). That was a race in the measurement, not a difference: the server stops reading after 1000 ms, which is also how long curl waits on `Expect: 100-continue`, and Curl's debug build starts more slowly, so its body chunk arrived after the server had stopped reading. Rerun with `-RequestReadMilliseconds 3000`: both send 336 bytes, byte-identical once the random boundary is masked. That is the head (with `Transfer-Encoding: chunked` and `Expect: 100-continue`), then a `6c` chunk holding the boundary line, `Content-Disposition: form-data; name="f"; filename="-"` and the blank line, and then the abort. With no difference found, there was nothing to fix and no test to add (criterion 2 holds with no task filed).
- Decision (sensible default): compare `Expect` uploads with a 3000 ms server read window so curl's 1 s Expect wait cannot race the server.
- Platform check: no code or test changed, so nothing new depends on the platform; build clean (0 warnings, 0 errors) and fast tests green here (46 test projects passed, none failed).

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 1bac223e e5faf1f9 c259902e.
- 2026-10-28: Doing -> Done. Real curl and Curl measured identical for -F f=@- with closed stdin: exit 42, same stderr, empty stdout, same request bytes
