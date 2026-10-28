---
id: BL-3984
title: CGR-110 part 4: the tool's -F f=@- streams stdin chunked and aborts with exit 42 when it is closed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs, Curl.Core.UnitLibrary/ClosedStandardInputStream.cs, Curl.Cli.UnitLibrary/ClosedStandardInputStream.cs, Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Curl.Cli.UnitLibrary/DiskDataFileReader.cs, Curl.Cli.UnitTests/Cgr110ClosedStandardInputStreamTests.cs, Curl.Cli.UnitTests/Cgr110DataOptionTests.cs, Curl.Console/Program.cs, Curl.Core.UnitTests/Cgr110FormTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3984 — CGR-110 part 4: the tool's -F f=@- streams stdin chunked and aborts with exit 42 when it is closed

## Goal

`curl -sS -F f=@- URL <&-` (standard input closed) sends the request head and the part's head chunked, then ends with exit 42 `curl: (42) operation aborted by callback`, as real curl 8.22.0 does, instead of sending an empty part and exiting 0.

## Context

Found by BL-3974 (CGR-110 part 3, Documentation/Reviews/Curl-Gap-Report-2026-10-25.md). The curl tool's `-F` body is built by `Curl.Core`'s `MultipartFormBodyBuilder.AddStandardInputPartAsync`, which copies standard input into memory up front and sends a `Content-Length`; it never reaches `Curl.Engine`'s `MimePostForm` read callback that BL-3974 changed. Measured 2026-10-28 with `Record-CurlExchange.ps1 -Curl "C:\Program Files\Git\bin\bash.exe" -CurlArgs '-c','curl -sS -F f=@- http://127.0.0.1:18974/ <&-; echo exit=$?'` (response `HTTP/1.1 200 OK`, `Content-Length: 5`, `hello`):
- real curl 8.22.0 (Schannel): exit 42, stderr `curl: (42) operation aborted by callback`, stdout empty (the response is not printed); on the wire `POST / HTTP/1.1`, `Host`, `User-Agent`, `Accept: */*`, `Transfer-Encoding: chunked`, `Content-Type: multipart/form-data; boundary=...`, `Expect: 100-continue`, then one chunk `6c` holding the boundary line, `Content-Disposition: form-data; name="f"; filename="-"` and the empty line, and nothing more.
- Curl: exit 0, `hello` printed, `Content-Length: 162` and a whole body with an empty part.

`Curl.Core` cannot see `Curl.Cli`'s `ClosedStandardInputStream`, and `Curl.Engine` must never reference `Curl.Cli` (Curl.Engine.UnitLibrary/CLAUDE.md), so this task first moves `ClosedStandardInputStream` from `Curl.Cli.UnitLibrary` to `Curl.Core.UnitLibrary` (namespace `Curl.Core`; the Cli files and `Program.cs` that name it take `using Curl.Core;`, its tests stay in `Curl.Cli.UnitTests` because they drive `DiskDataFileReader` and the parser). BL-3974's stash holds that move, already built green; redoing it is mechanical (`git mv`, the namespace line, a `using Curl.Core;` in CommandLineOptionTable.cs, CommandLineRefusal.cs, DiskDataFileReader.cs and the two Cgr110 test files, and `Curl.Core.ClosedStandardInputStream` in Program.cs). Record the move and the tool behaviour in an ADR (Decided by Claude under Stewart's delegation).

## Acceptance criteria

- [ ] `ClosedStandardInputStream` lives in `Curl.Core.UnitLibrary` (namespace `Curl.Core`), and `Curl.Cli` and `Curl.Console` use it from there.
- [ ] With standard input a `ClosedStandardInputStream`, the tool's `-F f=@-` sends the request head with `Transfer-Encoding: chunked` and `Expect: 100-continue` and the part's head, then ends with exit 42 and `curl: (42) operation aborted by callback`; a unit test in `Curl.Core.UnitTests/Cgr110FormTests.cs` fails before and passes after.
- [ ] The Record-CurlExchange comparison above is repeated for Curl and matches real curl's exit code, stderr and stdout; results recorded in Notes.
- [ ] dotnet build is clean and dotnet test --filter "TestCategory!=Integration" passes.

## Notes

## Log

- 2026-10-28: Created.
