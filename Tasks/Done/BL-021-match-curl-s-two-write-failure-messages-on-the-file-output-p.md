---
id: BL-021
title: Match curl's write-failure message suffix on the file:// output path
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-008]
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-25
completed: 2026-09-26
---
# BL-021 — Match curl's two write-failure messages on the `file://` output path

## Goal

A failed write to the download destination reports the byte counts curl 8.21.0 reports, so
`%{errormsg}` matches upstream instead of being a shortened paraphrase of it.

## Context

Measured on this machine against curl 8.21.0 (Release-Date 2026-06-24). Upstream has two
distinct exit 23 messages where this solution has one:

- a destination that stops accepting bytes part way through — a broken pipe, measured by
  piping a `file://` download into a command that exits early — prints
  `Failure writing output to destination, passed 16384 returned 0`, where the first number
  is the size of the chunk offered and the second is how many of those bytes the
  destination took;
- a write callback that returns a hard error prints
  `client returned ERROR on write of 10 bytes`.

`Curl.Protocol.File.UnitLibrary\FileTransferMessages.cs` has
`OutputWriteFailed = "Failure writing output to destination"` with no suffix, which
matches neither, and `FileProtocolHandler.CopyAsync` uses it for every output write
failure. Exit code 23 (`CURLE_WRITE_ERROR`) is unaffected and stays as it is
(<https://curl.se/libcurl/c/libcurl-errors.html>).

In this architecture the destination is `ITransferContext.Output`, a `Stream`:
`Stream.WriteAsync` either completes or throws, so a partial write is not observable and
"returned" is always 0 for the first form. The second form has no producer here at all —
nothing installs a write callback, because the `Output` stream *is* the callback — so it is
recorded rather than implemented, so that whoever adds a callback seam later finds the
exact text instead of inventing it.

## Acceptance criteria

- [x] `FileTransferMessages.OutputWriteFailed` becomes a method
      `OutputWriteFailed(long passed)` returning
      `$"Failure writing output to destination, passed {passed} returned 0"`, with an
      invariant-culture number, and its remarks state why `returned` is always 0 here.
- [x] `CopyAsync` passes the length of the chunk it offered, so a download whose output
      stream faults on the first write of a 40000-byte file reports
      `Failure writing output to destination, passed 16384 returned 0`; a test named
      `ExecuteAsync_OutputFailsOnFirstWrite_ReportsThePassedChunkSize` asserts the message
      verbatim and `CurlExitCode.WriteError`.
- [x] A second test asserts a short final chunk is reported with its own size: a
      1000-byte file whose output faults reports `passed 1000 returned 0`.
- [x] No second message form is implemented or documented. `client returned ERROR on
      write of <n> bytes` was **retracted on 2026-09-25**: the auditor's re-run found it
      came from its own malformed `-o` argument (a POSIX path given to a Windows binary
      with path conversion off), not from upstream. Do not re-add it, and do not record
      it as upstream behaviour. If a second form is ever wanted, measure it first and
      file its own task.
- [x] Every existing test asserting the old suffix-free message is updated, and the commit
      message names them.
- [x] `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

The upload destination's message is a different string
(`FileTransferMessages.DestinationWriteFailed`) and is unverified; that is BL-023. Do not
change it here beyond whatever `CopyAsync`'s signature change forces, so the two tasks stay
separable.

`FaultingStream.FailingOnWrite(int writeNumber)` in
`Curl.Protocol.File.UnitTests\Fakes` already provokes a write failure on a chosen write.

Delivered 2026-09-26 (dark factory lane 4). The change is one library and its tests, so
the `/feature` stages ran in-session: tests first, then the change, then build, fast tests
and `dotnet format --verify-no-changes`.

- `FileTransferMessages.OutputWriteFailed(long passed)` builds the message with an
  invariant-culture number; its remarks say why `returned` is always 0.
- `CopyAsync` now takes a `Func<long, string>` and passes it the length of the chunk it
  offered; the upload path passes `static _ => DestinationWriteFailed`, so BL-023's
  string is unchanged.
- Updated test: `ExecuteAsync_OutputStreamFails_ReportsWriteError` (ten-byte file, now
  `passed 10 returned 0`). New tests:
  `ExecuteAsync_OutputFailsOnFirstWrite_ReportsThePassedChunkSize` (40000 bytes,
  `passed 16384`), `ExecuteAsync_OutputFailsOnAShortFinalChunk_ReportsThatChunksOwnSize`
  (1000 bytes, `passed 1000`) and `ExecuteAsync_HeaderOutputFails_ReportsTheHeaderBlockSize`.
- Choice taken: the header-output failure also used the old constant, and the method
  forces it to pass a count. It passes the whole header block's length (90 bytes for a
  ten-byte file), because the block is written in one `WriteAsync`. That count is not
  measured against upstream, which writes each header line separately; BL-050 measures it.
  `TryWriteHeadersAsync` became `WriteHeadersAsync`, returning the failure or null, so
  the count stays where the headers are built.

## Log

- 2026-09-25: Created.
- 2026-09-25: Criterion 4 corrected. The second write-failure message was a mis-measurement (the auditor's own bad -o argument) and is retracted; the 'passed <n> returned 0' suffix in criteria 1-3 was measured correctly and stands.
- 2026-09-25: Title corrected from "two write-failure messages" to one; the file name keeps its original slug.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. file:// output write failures report curl's 'passed <n> returned 0' suffix with the offered chunk size
