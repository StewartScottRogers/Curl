---
id: BL-3985
title: CGR-110 part 4: the tool's -F f=@- streams stdin chunked and aborts with exit 42 when it is closed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3728, BL-3983]
touches: [Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs, Curl.Core.UnitLibrary/ClosedStandardInputStream.cs, Curl.Cli.UnitLibrary/ClosedStandardInputStream.cs, Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Curl.Cli.UnitLibrary/DiskDataFileReader.cs, Curl.Cli.UnitTests/Cgr110ClosedStandardInputStreamTests.cs, Curl.Cli.UnitTests/Cgr110DataOptionTests.cs, Curl.Console/Program.cs, Curl.Core.UnitTests/Cgr110FormTests.cs, Curl.Protocol.Abstractions.UnitLibrary/RequestBodyReadFailedException.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs, Curl.Protocol.Http.UnitTests/Cgr110FormAbortTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3985 — CGR-110 part 4: the tool's -F f=@- streams stdin chunked and aborts with exit 42 when it is closed

## Goal

`curl -sS -F f=@- URL <&-` (standard input closed) sends the request head and the part's head chunked, then ends with exit 42 `curl: (42) operation aborted by callback`, as real curl 8.22.0 does, instead of sending an empty part and exiting 0.

## Context

Found by BL-3974 (CGR-110 part 3, Documentation/Reviews/Curl-Gap-Report-2026-10-25.md). The curl tool's `-F` body is built by `Curl.Core`'s `MultipartFormBodyBuilder.AddStandardInputPartAsync`, which copies standard input into memory up front and sends a `Content-Length`; it never reaches `Curl.Engine`'s `MimePostForm` read callback that BL-3974 changed. Measured 2026-10-28 with `Record-CurlExchange.ps1 -Curl "C:\Program Files\Git\bin\bash.exe" -CurlArgs '-c','curl -sS -F f=@- http://127.0.0.1:18974/ <&-; echo exit=$?'` (response `HTTP/1.1 200 OK`, `Content-Length: 5`, `hello`):
- real curl 8.22.0 (Schannel): exit 42, stderr `curl: (42) operation aborted by callback`, stdout empty (the response is not printed); on the wire `POST / HTTP/1.1`, `Host`, `User-Agent`, `Accept: */*`, `Transfer-Encoding: chunked`, `Content-Type: multipart/form-data; boundary=...`, `Expect: 100-continue`, then one chunk `6c` holding the boundary line, `Content-Disposition: form-data; name="f"; filename="-"` and the empty line, and nothing more.
- Curl: exit 0, `hello` printed, `Content-Length: 162` and a whole body with an empty part.

`Curl.Core` cannot see `Curl.Cli`'s `ClosedStandardInputStream`, and `Curl.Engine` must never reference `Curl.Cli` (Curl.Engine.UnitLibrary/CLAUDE.md), so this task first moves `ClosedStandardInputStream` from `Curl.Cli.UnitLibrary` to `Curl.Core.UnitLibrary` (namespace `Curl.Core`; the Cli files and `Program.cs` that name it take `using Curl.Core;`, its tests stay in `Curl.Cli.UnitTests` because they drive `DiskDataFileReader` and the parser). BL-3974's stash holds that move, already built green; redoing it is mechanical (`git mv`, the namespace line, a `using Curl.Core;` in CommandLineOptionTable.cs, CommandLineRefusal.cs, DiskDataFileReader.cs and the two Cgr110 test files, and `Curl.Core.ClosedStandardInputStream` in Program.cs). Record the move and the tool behaviour in an ADR (Decided by Claude under Stewart's delegation).

## Acceptance criteria

- [x] `ClosedStandardInputStream` lives in `Curl.Core.UnitLibrary` (namespace `Curl.Core`), and `Curl.Cli` and `Curl.Console` use it from there.
- [x] With standard input a `ClosedStandardInputStream`, the tool's `-F f=@-` sends the request head with `Transfer-Encoding: chunked` and `Expect: 100-continue` and the part's head, then ends with exit 42 and `curl: (42) operation aborted by callback`; a unit test in `Curl.Core.UnitTests/Cgr110FormTests.cs` fails before and passes after.
- [x] The Record-CurlExchange comparison above is repeated for Curl and matches real curl's exit code, stderr and stdout; results recorded in Notes.
- [x] dotnet build is clean and dotnet test --filter "TestCategory!=Integration" passes.

## Notes

- 2026-10-28 (lane 5, before any code edit): exit 42 cannot come from Curl.Core alone. `HttpRequestBodyWriter.ReadAsync` maps every `RequestBodyReadFailedException` to exit 26 (`CurlExitCode.ReadError`), and the only exit-42 path is the trailer source. Plan: give `RequestBodyReadFailedException` an exit code (default `ReadError`) and have `HttpRequestBodyWriter` fail with it, so `MultipartFormBodyBuilder` can add the part head and then a callback segment that, on a `ClosedStandardInputStream`, throws it with `AbortedByCallback` and `operation aborted by callback`, with length unknown (chunked). The SMTP catcher in SmtpMailTransaction.cs keeps exit 26. Added RequestBodyReadFailedException.cs, HttpRequestBodyWriter.cs and an Http test file to touches. HttpRequestBodyWriter.cs is held by BL-3728 and Curl.Protocol.Abstractions.UnitLibrary by BL-3983 (both in Doing on origin/work/dark-factory), so this task now depends on them and goes back to Backlog. No other prerequisite was found: the chunked unknown-length body already comes from `MultipartBodySegments` (a null length), and the ClosedStandardInputStream move is mechanical.
- 2026-10-28 (lane 7): Done as planned. `ClosedStandardInputStream` moved to Curl.Core (namespace `Curl.Core`); `RequestBodyReadFailedException` now carries an `ExitCode` (default `ReadError`) and `HttpRequestBodyWriter` fails with it; `MultipartFormBodyBuilder.AddStandardInputPartAsync` adds the part head and then a callback segment of unknown length (a private `AbortedReadStream` deriving from `MemoryStream`, so only `Read(byte[], int, int)` needs overriding) whose read throws exit 42 `operation aborted by callback`. ADR-0967 records it. Tests: Curl.Core.UnitTests/Cgr110FormTests.cs (async and sync reads: part head, then exit 42) and Curl.Protocol.Http.UnitTests/Cgr110FormAbortTests.cs (the writer sends the chunk before the failure, then exit 42).
- Measured Curl with `Record-CurlExchange.ps1 -Port 18974 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello' -Curl bash.exe -CurlArgs '-c','<Curl.Console curl.exe> -sS -F f=@- http://127.0.0.1:18974/ <&-; echo exit=$?'`: exit 42, stderr `curl: (42) operation aborted by callback`, stdout empty (only the `exit=42` echo), matching real curl. The request head is `POST`, `Host`, `User-Agent`, `Accept`, `Transfer-Encoding: chunked`, `Content-Type: multipart/form-data; boundary=...`, `Expect: 100-continue`, as real curl's. In that run the recorder had stopped reading before Curl's 1-second `Expect` wait ended, so request.bin held only the head; again with `-RespondAfterBodyBytes 1` (the server waits for the body), the wire held the head and then exactly the `6c` chunk holding the boundary line, `Content-Disposition: form-data; name="f"; filename="-"` and the empty line, and nothing more, as real curl sent.
- Measure-CodeQuality for Curl.Core, Curl.Protocol.Abstractions, Curl.Protocol.Http, Curl.Cli and Curl.Console: one Core branch (`encoder?.Name` in the new branch) uncovered; fixed by formatting the part head once, and a second Core run reports 0 failing members. Full fast run: 46 test projects, 0 failures. The change is platform-neutral: the new tests use no paths, OS error text or certificates.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 65e5ab67 4380765b e0cd4d6e c259902e.
- 2026-10-28: Doing -> Backlog. Needs Curl.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs (held by BL-3728) and Curl.Protocol.Abstractions.UnitLibrary/RequestBodyReadFailedException.cs (held by BL-3983) to fail a body read with exit 42
- 2026-10-28: Backlog -> Doing. Criteria at claim: 65e5ab67 4380765b e0cd4d6e c259902e.
- 2026-10-28: Doing -> Done. -F f=@- with standard input closed sends the part head chunked and aborts with exit 42, as curl 8.22.0 does
