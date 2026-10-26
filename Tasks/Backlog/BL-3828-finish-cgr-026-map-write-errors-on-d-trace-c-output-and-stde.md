---
id: BL-3828
title: Finish CGR-026: map write errors on -D, --trace, -c, %output and --stderr files and the 16 KiB passed count as curl does
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3555]
touches: [Curl.Console/DumpHeaderOutputStream.cs, Curl.Console/UnhandledExceptionGuard.cs, Curl.Console.UnitTests/Cgr026FollowUpTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3828 — Finish CGR-026: map write errors on -D, --trace, -c, %output and --stderr files and the 16 KiB passed count as curl does

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

- [ ] Each command above, through Curl, gives curl 8.18.0's exit code and stderr text (pinned by unit tests in `Curl.Console.UnitTests/Cgr026FollowUpTests.cs` or the fixed library's own tests).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
