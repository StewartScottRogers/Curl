---
id: BL-111
title: Print curl's 'Failed writing headers to <file>' line when -D output fails
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-050]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-111 — Print curl's 'Failed writing headers to <file>' line when -D output fails

## Goal

When the `-D`/`--dump-header` output fails to write, Curl.Console prints curl's own `curl: Failed writing headers to <file>` line before the transfer's `curl: (23) ...` line, as curl 8.21.0 does.

## Context

Measured 2026-09-26 against curl 8.21.0 on Windows, in BL-050. With the header output a pipe whose reader had already exited:

```
{ sleep 0.3; curl -sS -D - -o body.txt file:///C:/Temp/bl050/ten.txt; } | true
```

exits 23 and prints exactly two stderr lines:

```
curl: Failed writing headers to -
curl: (23) client returned ERROR on write of 20 bytes
```

With `-v` the first line is followed by `* client returned ERROR on write of 20 bytes`.

- BL-050 made `Curl.Protocol.File.UnitLibrary`'s handler report the second line's message (`FileTransferMessages.HeaderWriteFailed(long passed)` in `Curl.Protocol.File.UnitLibrary/FileTransferMessages.cs`). This task does not change that library.
- The first line is the curl tool's own and belongs to `Curl.Console`; nothing in the solution prints "Failed writing headers" yet (grep finds none).
- The tool names the `-D` argument as given (`-` above). What it prints when `-D` names a file whose write fails is not yet measured; measure it against curl 8.21.0 (for example `-D` to a file on a full or read-only volume, or a device that rejects writes) and record the exact stderr in Notes before implementing, so the named-file case matches.
- Where to start: `Curl.Console/CurlCommandRunner.cs`. The precedent is `CurlCommandRunner.FailedWritingBodyLine` ("curl: Failed writing body") and `FormatErrorLine`, which print the tool's own line for a failed standard-output write. Note that with `-i` to a closed stdout curl prints only `curl: Failed writing body`, which is already handled; do not change that case.
- Test doubles already in `Curl.Console.UnitTests`: `FailingWriteStream.cs`, `InMemoryFileSystem.cs`, `RecordingProtocolHandler.cs`, `ClosedStandardOutputStreamTests.cs`.
- The line obeys `-s`/`-S` like other error lines (`ShowsErrors` in `CurlCommandRunner`): printed with `-sS`, suppressed with `-s` alone.
- Curl.Console is held to 100% line and branch coverage and cyclomatic complexity of at most 10 per method (root `CLAUDE.md`, Quality gates).

## Acceptance criteria

- [ ] The curl 8.21.0 stderr for a `-D <named file>` whose header write fails is measured and recorded verbatim in this task's Notes, with the command used.
- [ ] A test in `Curl.Console.UnitTests` drives `-sS -D - -o body.txt` over a transfer whose header write to standard output fails with the file handler's `client returned ERROR on write of 20 bytes` (exit `CurlExitCode.WriteError`, 23), and asserts stderr is exactly `curl: Failed writing headers to -` followed by `curl: (23) client returned ERROR on write of 20 bytes`, and nothing else.
- [ ] A test in `Curl.Console.UnitTests` covers the named-file case and asserts the stderr wording recorded in Notes.
- [ ] A test asserts that with `-s` alone neither line is printed and the exit code is still 23.
- [ ] The existing `-i` to closed stdout behaviour (`curl: Failed writing body` only) still passes unchanged.
- [ ] `dotnet build Curl.Console -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Blocked. Stewart: dark factory run ended in Doing, exit 1; see logs\BL-111-20260926-083111-L4.jsonl
- 2026-09-26: Blocked -> Backlog. Not blocked: the 2026-09-26 shift ran out of tokens (usage limit), which it misfiled as a stall
- 2026-09-26: Backlog -> Doing.
