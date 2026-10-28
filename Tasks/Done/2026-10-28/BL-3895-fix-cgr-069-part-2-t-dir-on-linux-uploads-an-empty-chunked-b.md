---
id: BL-3895
title: Fix CGR-069 part 2: -T dir on Linux uploads an empty chunked body and exits 0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3868]
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/Cgr069UploadDirectoryTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3895 — Fix CGR-069 part 2: -T dir on Linux uploads an empty chunked body and exits 0

## Goal

On Linux, `curl -sS -T sub http://127.0.0.1:PORT/a` behaves as curl 8.22.0 does: same exit code, stdout bytes, stderr text and request bytes on the wire (CGR-069).

## Context

Part of BL-3598 (CGR-069, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`). Expected real curl: sends `PUT /a` chunked with only `0

` and exits 0. Measure curl first with `Record-CurlExchange.ps1`; re-check against 8.22.0.

## Acceptance criteria

- [x] `curl -sS -T sub http://127.0.0.1:PORT/a` through real curl and through Curl gives the same exit code, stdout bytes, stderr text and request bytes; commands and both results recorded in Notes.
- [x] A focused unit test in the named `.UnitTests` file fails before the fix and passes after it (ADR-0482).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- 2026-10-28 (lane 7): real curl measured: `Record-CurlExchange.ps1 -Port 18395 -ListenAddress 172.26.96.1 -Curl C:\Windows\System32\wsl.exe -CurlArgs '-d','Ubuntu','--cd','/home/stewart_rogers/bl3895','--','curl','-sS','-T','sub','http://172.26.96.1:18395/a'` (curl 8.18.0 OpenSSL, the only Linux curl on this machine; no 8.22.0 Linux build is available, so 8.18.0 stands in for it): exit 0, stdout and stderr empty, request `PUT /a HTTP/1.1`, Host, `User-Agent: curl/8.18.0`, `Accept: */*`, `Transfer-Encoding: chunked`, `Expect: 100-continue`, blank line, `0\r\n\r\n`. (`pwsh -File` mangled the comma-list CurlArgs and `/tmp` did not survive between WSL starts: call the script with `&` and use a home directory.)
- Fix (ADR-0962): off Windows, a `-T` path whose open fails with `FileAccessStatus.IsDirectory` is uploaded as an empty `StandardInputUploadStream` of unknown size, the stream `-T -` uses (`CurlCommandRunner.UploadsDirectoryAsEmptyStream`, used by `TransferOpeningUploadFileAsync` and the `-Z` batch check `UploadFileOpensAsync`). Curl on Windows cannot take that branch, so its wire bytes were checked through the same stream: `Record-CurlExchange.ps1 -Port 18399 -RequestReadMilliseconds 4000 -Curl <built curl.exe> -CurlArgs '-sS','-T','-','http://127.0.0.1:18399/a'` with empty stdin: exit 0, stdout and stderr empty, the same request bytes (User-Agent `curl/8.22.0` aside). Without `-RequestReadMilliseconds` the recorder answered before the body after the 1 s Expect wait and missed the last chunk; `-v` showed Curl sent its 5 bytes.
- `Cgr069UploadDirectoryTests` (temp directory, `PhysicalFileSystem`, `runsOnWindows` false/true): with `UploadsDirectoryAsEmptyStream` forced to false the two off-Windows tests failed; with the fix all 3 pass. The test is platform-neutral (a temporary directory and the runner's `runsOnWindows` flag; no OS error text).
- Build clean, fast tests green; `Measure-CodeQuality.ps1 -Library Curl.Console`: 0 failing members.

- Split from BL-3598 (over its cost cap).
- 2026-10-28 (lane 5): the `-T` file is opened in `Curl.Console/CurlCommandRunner.cs` `TransferOpeningUploadFileAsync` (`fileSystem.OpenForReadAsync`; a failed open prints `curl: cannot open` and exits 26), not in `Curl.Cli.UnitLibrary/CommandLineRefusal.cs`, and `Curl.Cli.UnitTests` cannot reach the runner, so touches now name that file and a new `Curl.Console.UnitTests/Cgr069UploadDirectoryTests.cs`. BL-3868 (Doing) holds `CurlCommandRunner.cs`, so this task depends on it.
- Planned fix: off Windows (`runsOnWindows` false), when the open fails with `FileAccessStatus.IsDirectory` (or the path is a directory), upload an empty, non-seekable stream of unknown size, as standard input is uploaded, so HTTP sends `Transfer-Encoding: chunked` and only the last chunk `0\r\n\r\n`. Why: curl opens the directory with open(2) (succeeds on Linux), fstat says it is not a regular file so the size stays unknown, and `tool_read_cb` turns read(2)'s EISDIR into end of file. Keep `PhysicalFileSystem.OpenForReadAsync` as it is: the file:// handler relies on its directory status. Re-check the expected bytes against Linux curl 8.22.0 (WSL) with Record-CurlExchange.ps1 before pinning.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: e7706e08 a7c0fc04 779790c0.
- 2026-10-28: Doing -> Backlog. Needs Curl.Console/CurlCommandRunner.cs, held by BL-3868 in Doing; depends-on BL-3868
- 2026-10-28: Backlog -> Doing. Criteria at claim: e7706e08 a7c0fc04 779790c0.
- 2026-10-28: Doing -> Done. -T dir off Windows uploads an empty chunked body and exits 0, as Linux curl does
