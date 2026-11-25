---
id: BL-5890
title: Fix fitness finding STD-209: -J with a Content-Disposition filename of conin$ exits 2 with ".NET" text "Cannot access a closed file."; curl exits 23
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-5915]
touches: [Curl.Console/DeferredOutputFileStream.cs, Curl.Console.UnitTests/DeferredOutputFileStreamTests.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5890 — Fix fitness finding STD-209: -J with a Content-Disposition filename of conin$ exits 2 with ".NET" text "Cannot access a closed file."; curl exits 23

## Goal

STD-209 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: -J with a Content-Disposition filename of conin$ exits 2 with ".NET" text "Cannot access a closed file."; curl exits 23. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (3 runs) · **Area:** HTTP `-O -J` output file handling, Windows build.
- **Mock:** an HTTP server whose `/cd/<urlsafe-base64>` answers 200 with `Content-Disposition: <decoded value>` and body `DATA\n`.
- **Repro** (in an empty directory): `curl -sS -OJ http://127.0.0.1:34000/cd/YXR0YWNobWVudDsgZmlsZW5hbWU9ImNvbmluJCI -w "[%{filename_effective} %{exitcode}]\n" < NUL` (the path encodes `attachment; filename="conin$"`).
- **Expected (REF 8.22.0):** stderr `curl: (23) Failed writing body`, stdout `[conin$ 23]`, exit 23.
- **Actual (.NET):** stderr `curl: (2) Cannot access a closed file.` (an ObjectDisposedException message), no `-w` output, exit 2. Plain `-o conin$` correctly exits 23.
- **Likely location:** the `-J` path in `Curl.Engine.UnitLibrary/EngineToolTransferOutputs.cs` (the remote-header-name stream wrapping): the stream is disposed after the open or write fails and is then used again.
- **Acceptance check:** the repro exits 23, prints `[conin$ 23]` on stdout, and stderr contains no .NET exception text.

## Acceptance criteria

- [x] the repro exits 23, prints `[conin$ 23]` on stdout, and stderr contains no .NET exception text.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

2026-11-24, lane 11 (measured and diagnosed; no code edited).

- `touches` changed from `Curl.Engine.UnitLibrary/EngineToolTransferOutputs.cs` (the finding's guess; that
  record holds no stream logic) to the two `Curl.Console` files where the fault is. Waiting on BL-5915, which
  is in Doing and names the whole `Curl.Console.UnitTests`.
- Reproduces on this branch: `-sS -OJ`, `-OJ`, and `-sS -OJ` with `-i` and an empty body exit 2 with
  `curl: (2) Cannot access a closed file.`; `-sS -i -OJ` with a 5-byte body says `closed Stream`.
- Real curl 8.22.0 Schannel (`Record-CurlExchange.ps1`, `filename="conin$"`,
  `-w "[%{filename_effective} %{exitcode} %{size_download}]\n"`):
  - 5-byte body, `-sS -OJ`, `-OJ` or `-sS -i -OJ`: exit 23, `[conin$ 23 5]`, stderr
    `curl: curl: (23) Failed writing body` (the doubled `curl: ` is real, already
    `CurlCommandRunner.OutputFileCloseFailureLine`; the finding's single prefix is wrong).
  - Empty body: `-OJ` exits 0; `-i -OJ` exits 23, `[conin$ 23 0]`, the same close-failure line.
  - 20000-byte body: exit 23, `[conin$ 23 0]`,
    `curl: (23) Failure writing output to destination, passed 16384 returned 0`. Curl already does this but
    for the number (20000, its own chunk size; out of scope).
  - Plain `-o conin$`, 5-byte body: real curl also prints the close-failure line; Curl prints
    `passed 5 returned 0` with exit 23 (`[conin$ 23 0]`). Same stream; check it after the fix.
- Cause (`DeferredOutputFileStream`): with the file already open (`-J` opens it from the header line),
  `WriteToOpenFile` buffers the write and `FlushIgnoringFailure` swallows the flush's `IOException`; .NET
  has by then closed the invalid console handle, so the next `Write`, `Flush`/`FlushAsync` (`-N`) or
  `TryFlushAsync` throws `ObjectDisposedException`, which nothing catches.
- Fix to make: `TryFlushAsync` returns false for `ObjectDisposedException` too (gives the close-failure
  line); `WriteToOpenFile` on a stream whose `CanWrite` is false accepts a write smaller than the 4096-byte
  buffer (C stdio holds it and `fclose` reports the failure) and fails a larger one with `IOException`;
  `Flush` and `FlushAsync` do nothing on such a stream. Unit test with a fake stream whose flush throws
  `IOException` and closes itself (platform-neutral; no real `conin$`).

2026-11-24, lane 12 (fixed).

- `DeferredOutputFileStream`: once a failed flush has closed the file (`CanWrite` false), a write under
  4096 bytes succeeds and a larger one fails with `IOException` (`WriteToClosedFile`), `Flush` and
  `FlushAsync` do nothing, and `TryFlushAsync` returns false, so `CompleteAsync` gives the close-failure
  line. Chose a `CanWrite` check over catching `ObjectDisposedException`: no exception path, fewer
  branches. ADR-1371 records it.
- Unit tests `CompleteAsync_JNamedFileClosedByAFailedFlush_ReportsTheCloseFailure` and
  `WriteAsync_FileClosedByAFailedFlush_FailsAWriteTheStdioBufferCannotHold`, with a `MemoryStream` that
  closes itself on its first flush, reached through the `openUnderRawBytes` hook (platform-neutral).
- Repro with `Record-CurlExchange.ps1` against the built `curl.exe`: `-sS -OJ`, `-OJ` and `-sS -i -OJ` with
  a 5-byte body each exit 23, stdout `[conin$ 23]`, stderr `curl: curl: (23) Failed writing body` (the
  doubled prefix is real curl 8.22.0's, measured by lane 11).
- Fast tests green; `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line, 100% branch, 0 failing.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: c6a727e1 69116afe 3d836d5e.
- 2026-11-24: Doing -> Backlog. Waiting on BL-5915 (in Doing), whose touches names Curl.Console.UnitTests, where this fix's unit test goes; cause found in Curl.Console/DeferredOutputFileStream.cs and real curl measured (see Notes)
- 2026-11-24: Backlog -> Doing. Criteria at claim: c6a727e1 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. -OJ with a Content-Disposition name of conin$ exits 23 with curl's Failed writing body line and -w output; a file closed by a failed flush no longer throws ObjectDisposedException (ADR-1371).
