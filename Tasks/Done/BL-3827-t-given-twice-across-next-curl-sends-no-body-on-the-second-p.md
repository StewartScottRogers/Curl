---
id: BL-3827
title: -T - given twice across --next: Curl sends no body on the second PUT where curl sends the stdin bytes
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests/StandardInputUploadEarlyResponseTests.cs, Curl.Console.UnitTests/StandardInputUploadTwiceAcrossNextTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3827 — -T - given twice across --next: Curl sends no body on the second PUT where curl sends the stdin bytes

## Goal

`curl -s -T - URL/a --next -T - URL/b` with stdin `abc`, against a server that answers `200 OK` with `Connection: close` before reading the body, sends the same bytes on the wire as curl 8.22.0.

## Context

Found by BL-3546 (CGR-017) while measuring stdin repros with Record-CurlExchange.ps1 (-Response twice, Connection: close, -StandardInput abc). Real curl sends both PUT heads (chunked, Expect: 100-continue); the server answers /a before the body, curl does not consume stdin there, and the second PUT carries `3\r\nabc\r\n0\r\n\r\n`. Curl sends both heads and no body at all: stdin appears consumed (or the upload abandoned) by the first transfer. Exit codes and stdout match (0). The report notes `-T -` twice as not failing; this is a bytes-on-the-wire difference only. Where the stdin upload is read and abandoned on an early final response is not yet pinned: start in Curl.Protocol.Http.UnitLibrary and narrow `touches` once found.

Unblocked 2026-10-29 (decided by Claude under Stewart's delegation): the first attempt stopped because real curl's answer varied with timing. Start by making the repro deterministic: extend `Record-CurlExchange.ps1` with a mode that holds the early `200` until curl's `Expect: 100-continue` wait has expired (curl's default is 1 s), so curl has begun sending the body before the answer arrives, then measure real curl several times in that mode. If real curl then gives one stable answer, pin it and fix Curl to match. If it still varies, record the runs in Notes and complete the task as not reproducible: a byte difference that curl itself does not hold steady is not a drop-in gap.

## Acceptance criteria

- [x] The command above, through real curl and Curl via Record-CurlExchange.ps1, gives the same exit code, stdout, stderr and request bytes; recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- 2026-10-28 measurement (Record-CurlExchange.ps1, `-s -T - URL/a --next -T - URL/b`, stdin `abc`, two `Connection: close` 200 responses, `-Connections 2`): the difference does not reproduce deterministically. Real curl 8.22.0 itself varies with timing: with `-RespondAfterBodyBytes 0` it sent no body on either PUT in 4 of 4 runs (150-550 ms), yet a single-PUT run of the same setup took ~5.5 s and sent `3\r\nabc\r\n0\r\n\r\n`. Without early response (`-RequestReadMilliseconds 3000`) curl and Curl are byte-identical: `/a` gets `3 abc 0`, `/b` gets an empty chunked body (`0`). In no run did real curl send `abc` on the second PUT, and Curl never sent a bodiless second PUT; the task's reported bytes did not appear. With no stable real-curl answer to pin, no fix and no failing unit test could be written. Needs a deterministic repro (a recorder mode that holds the 200 until the 100-continue wait has expired) before another lane tries it.
- 2026-10-29 deterministic measurement (decided by Claude under Stewart's delegation): the held-200 mode needed no new code. `-RespondAfterBodyBytes 0 -ResponseDelayMilliseconds 1500` already reads the head, waits 1.5 s (past curl's 1 s 100-continue wait, so curl starts the body), then answers and records what curl still sends; Record-CurlExchange.ps1's RespondAfterBodyBytes help now says so. Command: `-s -T - http://127.0.0.1:P/a --next -T - http://127.0.0.1:P/b`, stdin `abc`, two `HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n` responses, `-Connections 2`. Real curl 8.22.0 (Schannel), 4 of 4 runs: exit 0, empty stdout and stderr, `/a` carries `3\r\nabc\r\n0\r\n\r\n` and `/b` an empty chunked body `0\r\n\r\n`. Curl (`-Curl dotnet run --project Curl.Console --no-build`), 2 of 2 runs: exit 0, empty stdout and stderr, request bytes identical to real curl's in full (ports aside). The reported difference (curl sending `abc` on the second PUT, Curl sending no body) does not occur in the deterministic mode: curl holds one stable answer and Curl already gives it.
- Criterion 2 is ticked as not applicable: with no difference there is no fix, so no test can fail before one. Per the unblock note, the task completes as "Curl already matches the stable answer" rather than with a code change; Curl.Protocol.Http.UnitLibrary and the two named test files were left untouched.
- Criterion 3: `dotnet build` clean and fast tests green on Windows; the only change is help text in Record-CurlExchange.ps1, which is platform-neutral.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 84b47d48 0ee60237 779790c0.
- 2026-10-28: Doing -> Blocked. Stewart: BL-3827 does not reproduce: real curl 8.22.0 varies run to run on the early-response -T - PUT, and Curl matches it where it is stable; can it be closed as not reproducible?
- 2026-10-29: Blocked -> Backlog. Unblocked by Stewart; first make the repro deterministic with a held-200 recorder mode, else close as not reproducible
- 2026-10-29: Backlog -> Doing. Criteria at claim: 84b47d48 0ee60237 779790c0.
- 2026-10-29: Doing -> Done. Deterministic held-200 measurement: real curl 8.22.0 stably sends abc on /a and an empty chunked body on /b; Curl already sends identical bytes
