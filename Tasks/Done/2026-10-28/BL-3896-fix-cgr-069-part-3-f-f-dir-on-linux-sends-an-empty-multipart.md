---
id: BL-3896
title: Fix CGR-069 part 3: -F f=@dir on Linux sends an empty multipart part and exits 0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Curl.Cli.UnitTests/Cgr069FormFileTests.cs, Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs, Curl.Core.UnitLibrary/CLAUDE.md, Curl.Core.UnitTests/Multipart/MultipartFormBodyBuilderTests.cs, Curl.Core.UnitTests/Multipart/MultipartFormBodyBuilderMissingFileTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3896 — Fix CGR-069 part 3: -F f=@dir on Linux sends an empty multipart part and exits 0

## Goal

On Linux, `curl -sS -F f=@sub http://127.0.0.1:PORT/a` behaves as curl 8.22.0 does: same exit code, stdout bytes, stderr text and request bytes on the wire (CGR-069).

## Context

Part of BL-3598 (CGR-069, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`). Expected real curl: sends a complete multipart body with an empty part (`filename="sub"`) and exits 0. Measure curl first with `Record-CurlExchange.ps1`; re-check against 8.22.0.

## Acceptance criteria

- [x] `curl -sS -F f=@sub http://127.0.0.1:PORT/a` through real curl and through Curl gives the same exit code, stdout bytes, stderr text and request bytes; commands and both results recorded in Notes.
- [x] A focused unit test in the named `.UnitTests` file fails before the fix and passes after it (ADR-0482).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3598 (over its cost cap).
- 2026-10-28 (lane 5): added Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs, Curl.Core.UnitLibrary/CLAUDE.md and the two Core multipart test files to touches: the `-F` body is built (and the directory refused) in Core's `MultipartFormBodyBuilder`, not the Cli parser; no task in Doing on origin/work/dark-factory names them. CommandLineRefusal.cs needed no change.
- Fix: `MultipartFormBodyBuilder.SendsDirectoryEmpty` (init, default `!OperatingSystem.IsWindows()`) makes a file part whose open fails with `FileAccessStatus.IsDirectory` go as its headers (file name kept) and no data, the length unknown, so the HTTP framing sends it chunked with `Expect: 100-continue`, exactly as curl does. Windows keeps exit 26 `read error getting mime data`. The default lives in the builder, not in `CurlCommandRunner.cs` (held by BL-3868), so the tool and the libcurl API (`ManagedEasyTransfer`, same libcurl behaviour on Linux) both get it. The two existing Core tests that pin the refusal with the default builder are now `[OSCondition(OperatingSystems.Windows)]`. No ADR: this matches measured curl and involves no design choice.
- Measured on WSL (curl 8.18.0, the only Linux curl here; no 8.22.0 for Linux on this machine): `mkdir sub; ./Record-CurlExchange.sh [-Curl <Curl linux-x64 self-contained publish>/curl] -Port 20358 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nok\n' -OutDirectory <dir> -CurlArgs -sS -F f=@sub http://127.0.0.1:20358/a`. Real curl: exit 0, stdout `ok\n`, stderr empty, request `POST /a` with `Transfer-Encoding: chunked`, multipart Content-Type, `Expect: 100-continue`, one `cc` chunk holding the part (`name="f"; filename="sub"`, `Content-Type: application/octet-stream`, no data) and the closing boundary, then `0`. Curl (this commit, published for linux-x64 and run in WSL): exit 0, stdout `ok\n`, stderr empty, and the request byte-identical once the random boundary and the User-Agent version (8.18.0 vs 8.22.0) are normalised (`cmp` clean).
- Focused tests in Curl.Cli.UnitTests/Cgr069FormFileTests.cs (the file the criterion names; the Cli tests reach Core): directory off Windows sends the empty part chunked, with an encoder sends an empty encoded part, a missing file still fails with the open error, and a directory on the Windows build fails with the read error. With the new branch disabled, `Build_FilePartNamingADirectoryOffWindows_SendsAnEmptyPartChunked` failed; with it, all 4 pass.
- `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`: 0 failing members (the first run flagged the async method at complexity 12, so the unopened-file path became its own `AddUnopenedFilePartAsync`; the measure does not union branch outcomes across test projects, so the Cli test file also covers the missing-file outcome). Platform-neutral: each test sets `SendsDirectoryEmpty` itself and uses its own temp directory.
- `dotnet build` clean; fast tests 46 assemblies green (44,509 passed, 308 skipped by platform).

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: f81d27c5 a7c0fc04 779790c0.
- 2026-10-28: Doing -> Done. On Linux and macOS, -F f=@dir sends an empty file part chunked and exits 0, as curl does
