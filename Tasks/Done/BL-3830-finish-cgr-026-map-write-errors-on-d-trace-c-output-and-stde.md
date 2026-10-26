---
id: BL-3830
title: Finish CGR-026: map write errors on -D, --trace, -c, %output and --stderr files and the 16 KiB passed count as curl does
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3555]
touches: [Curl.Console/DumpHeaderOutputStream.cs, Curl.Console/UnhandledExceptionGuard.cs, Curl.Console.UnitTests/Cgr026FollowUpTests.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console/TransferEventOutput.cs, Curl.Console/StandardErrorFailureIgnoringStream.cs, Curl.Console/WriteFailureIgnoringStream.cs, Curl.Console/WriteFailureIgnoringWriteOutFileOpener.cs, Curl.Console/Program.cs, Curl.Console/CLAUDE.md, Curl.Console.UnitTests/Cgr008Tests.cs, Curl.Cookies.UnitLibrary/CookieStore.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3830 — Finish CGR-026: map write errors on -D, --trace, -c, %output and --stderr files and the 16 KiB passed count as curl does

## Goal

The rest of third-party gap report issue CGR-026 (see `Tasks/Done/*/BL-3555-*` or BL-3555 in Doing/Done for the full issue text) matches curl 8.18.0 on Linux: write errors on `-D`, `--trace`, `-c`, `%output{}` and `--stderr` files, and the byte count of a large body's write failure.

## Context

BL-3555 fixed the `-o` file (flushed in `DeferredOutputFileStream.CompleteAsync`; `curl: curl: (23) Failed writing body`, exit 23, `%{errormsg}` `Failed writing received data to disk/application`). Real curl 8.18.0 in WSL, measured 2026-10-26 with `file:///tmp/s.txt` (12 bytes) and `-sS`:
- `-D /dev/full -o /dev/null`: `curl: Failed writing headers to /dev/full` then `curl: (23) client returned ERROR on write of 17 bytes` (for HTTP), exit 23. Curl gives exit 2: the `-D` file's buffered FileStream likely throws again when disposed.
- `--trace /dev/full -o /dev/null`, `-c /dev/full -o /dev/null` (HTTP), `-o /dev/null -w '%output{/dev/full}x'`: exit 0, nothing on stderr. Curl gives exit 2 with `No space left on device`.
- `--stderr /dev/full http://127.0.0.1:<closed port>/`: exit 7. Curl gives exit 2.
- `-o /dev/full` with a 20000-byte body: curl says `(23) Failure writing output to destination, passed 16384 returned 0`; Curl says `passed 16342`.
WSL has curl 8.18.0 (`wsl -e curl`) and `/dev/full`; Curl itself can only be driven on Windows, so pin it with unit tests using a stream that fails flush/dispose (see `Curl.Console.UnitTests/Cgr026Tests.cs`'s `FullDeviceFileSystem`). The cookie jar write is in `Curl.Engine` (`TransferPerformer` / `CookieEngine`): add those files to `touches` if the fix lands there.

## Acceptance criteria

- [x] Each command above, through Curl, gives curl 8.18.0's exit code and stderr text (pinned by unit tests in `Curl.Console.UnitTests/Cgr026FollowUpTests.cs` or the fixed library's own tests).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- 2026-10-26 (lane 9): Measured again with curl 8.18.0 in WSL, `file:///tmp/s.txt` (12 bytes), `-sS`: `-D /dev/full -o /dev/null` -> `curl: Failed writing headers to /dev/full`, `curl: (23) client returned ERROR on write of 20 bytes` (20 = file://'s `Content-Length: 12` line; 17 over HTTP), exit 23; `--trace`, `--trace-ascii`, `%output{/dev/full}` -> exit 0, nothing on stderr; `--stderr /dev/full` to port 1 -> exit 7; `-o /dev/full` with a 20000-byte `file://` source -> `passed 16384 returned 0`; over HTTP with `nc` sending head and body in one buffer -> `passed 16342 returned 0`.
- Decision (ADR-0818): `StandardErrorFailureIgnoringStream` renamed `WriteFailureIgnoringStream` and made to ignore a failed close too; the `--stderr` file, the trace file and every `%output{}` target (new `WriteFailureIgnoringWriteOutFileOpener` around the runner's opener) go through it; the `-D` file is closed through it (its writes still fail via `DumpHeaderOutputStream`); `CookieStore.SaveCookieJarAsync` returns false when the jar's close fails. The 16 KiB count needs no change: curl itself says 16342 when head and body arrive in one read, so the report's 16384 came from its Python server's separate segments; Curl's existing `HttpResponseBodyReaderTests` pin 16384 for a body after the head, and `Cgr026FollowUpTests` pins 16384 for `file://`.
- Added to touches (no task in Doing on origin/work/dark-factory names them): `CurlCommandRunner.cs` (`-D` close, `--stderr` file, write-out opener), `TransferEventOutput.cs`, the renamed stream and its users (`Program.cs`, `Cgr008Tests.cs`, `CLAUDE.md`), the new opener, and `Curl.Cookies.UnitLibrary/CookieStore.cs` (the `-c` jar is saved there, not in Curl.Engine). `DumpHeaderOutputStream.cs` and `UnhandledExceptionGuard.cs` needed no change.
- `-c` is pinned on `CookieStore.SaveCookieJarAsync` directly (the engine's `WriteCookieJarAsync` only calls it and ignores its result).
- Build clean, fast tests green (46 test assemblies, none failed). `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Cookies.UnitLibrary`: 0 failing members. Tests use in-memory streams only, no paths or OS text, so they are platform-neutral.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 39cc2287 779790c0.
- 2026-10-26: Doing -> Done. A -D, --trace, -c, %output or --stderr file on a full device now gives curl 8.18.0's exit code and stderr instead of exit 2
