---
id: BL-024
title: Close the untested-decision gaps in Curl.Protocol.File.UnitTests
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-008, BL-016, BL-017, BL-021, BL-023]
requirement: none
created: 2026-09-25
completed:
---
# BL-024 — Close the untested-decision gaps in `Curl.Protocol.File.UnitTests`

## Goal

Every decision `FileProtocolHandler` makes is exercised by a test, so the branches the
implementation chose cannot change silently.

## Context

`Curl.Protocol.File.UnitTests` has 76 `[TestMethod]`s — 47 on the handler, 29 on
`FileUrlPath`, 82 test cases once `[DataRow]`s are counted — all passing, and the code
review still found choices in
`Curl.Protocol.File.UnitLibrary\FileProtocolHandler.cs` that no test reaches. Each item
below is a branch or an invariant in that file, not a hypothetical. The fakes needed
already exist in `Curl.Protocol.File.UnitTests\Fakes`: `FakeFileSystem` (with `Calls`,
`WrittenBytes`, `AddFileReadingFrom`, `FailOpenForWrite`), `FaultingStream`
(`FailingOnRead`, `FailingOnWrite`), `NonSeekableStream`, `TrackedMemoryStream`
(`WasDisposed`) and `ChunkRecordingStream` (`WriteLengths`). One new fake is needed: a
stream that returns fewer bytes than asked for without being at its end, because every
current fake is backed by a `MemoryStream`, which always returns the full count.

## Acceptance criteria

Surviving mutants found by the BL-008 re-review on 2026-09-25. Each is a change that can
be made to production code with all tests still green, so each needs a test that fails:

- [ ] Replacing the cancellation token with `CancellationToken.None` at the
      `IFileSystem.OpenForReadAsync` and `OpenForWriteAsync` calls must fail a test. Have
      `FakeFileSystem` capture the token per call and assert it equals the context's, in
      one download and one upload test.
- [ ] The same for the header write, which also accepts `CancellationToken.None` today.
- [ ] Deleting the `await using` around the upload destination must fail a test. The
      download side already fails three; the upload side fails none, so an `IDisposable`
      leak there would ship unnoticed. Use `FakeFileSystem.WriteInto` with a
      `TrackedMemoryStream` and assert `WasDisposed`, plus a variant where the write fails.
- [ ] The header-write failure path (exit 23 with the output-write message, before any
      body byte) must be covered. `ExecuteAsync_OutputStreamFails_ReportsWriteError` does
      not reach it because its `HeaderOutput` is null.
- [ ] `CancellingStream` with `StreamCancellationStyle.FaultedValueTask` and
      `cancels: null` throws `ArgumentOutOfRangeException` rather than cancelling, because
      `ValueTask.FromCanceled` rejects an uncancelled token. Its own XML documentation
      advertises that combination, so fix the fake to return
      `ValueTask.FromException(new TaskCanceledException())` in that case, or state the
      restriction in the remarks.
- [ ] `ExecuteAsync_UploadWithPositiveResumeFrom_OpensTheDestinationAppending` asserts
      only the open mode. Add a test driving a positive resume into a destination that
      already holds bytes, which is the end-to-end resume semantics.

Each item is one or more named `[TestMethod]`s in `Curl.Protocol.File.UnitTests`, asserting
the handler's observable outcome (`TransferResult` plus the bytes and calls the fakes
recorded):

- [ ] `ResumeFrom` and `Range` supplied together: assert `TryResolveWindow`'s documented
      precedence (`ResumeFrom` wins, `Range` ignored). The test name and a comment must say
      this combination is unreachable from the command line — curl makes `-C` and `-r`
      mutually exclusive, exit 2, per BL-013 — so nobody reads it as upstream conformance.
- [ ] An upload with `ResumeFrom`: assert `FakeFileSystem.Calls` records
      `FileWriteMode.Append`, and that a seekable upload source is seeked; then the same
      with a `NonSeekableStream` source, asserting the skip happens by reading and the
      remaining bytes land in the destination.
- [ ] A suffix range longer than the file: `ByteRange.Suffix(100)` against a 10-byte file
      transfers all 10 bytes, exit 0.
- [ ] Every range form against a zero-length file: `Suffix`, `FromOffset(0)` and
      `Bounded(0, 4)`, each asserting the exit code and byte count the implementation
      produces.
- [ ] A `Bounded` range whose end is past the end of the file (`Bounded(2, 99)` against a
      10-byte file) transfers bytes 2 to 9, and one whose start equals the length
      (`Bounded(10, 12)`) is a success transferring nothing, exit 0 — the boundary between
      "equal is fine" and "past the end is exit 36".
- [ ] `NoBody` together with a non-null `HeaderOutput`, which is the real `-I` case: the
      exact header bytes reach `HeaderOutput`, `Output` receives nothing, exit 0.
- [ ] A `HeaderOutput` stream that fails on write: exit 23 with the message
      `FileTransferMessages` gives for an output write failure, and no body written.
- [ ] A mid-transfer failure writing to an upload destination, so
      `FileTransferMessages.DestinationWriteFailed` is asserted through a
      `TransferResult.ErrorMessage` somewhere in the suite.
- [ ] A partial-read source: a new fake stream returns fewer bytes than requested on one
      read without being at its end, and the test asserts the whole file still arrives —
      this is `CopyAsync`'s "a short read is not end-of-stream" branch, currently untested.
- [ ] Upload-destination disposal on all three paths — success, a write failure and a skip
      failure — asserted through `TrackedMemoryStream.WasDisposed`.
- [ ] The `--time-cond` equality boundary in both directions, asserting the behaviour
      BL-017 establishes (at equality neither direction transfers), so a `>` versus `>=`
      slip fails a test.
- [ ] `ExecuteAsync_FileLargerThanOneChunk_WritesAFullChunkFirst` is strengthened to
      assert the whole determined sequence: `LargeContent()` is 40000 bytes, so
      `ChunkRecordingStream.WriteLengths` must be exactly `[16384, 16384, 7232]`.
- [ ] `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"` is
      green, no test carries `[TestCategory("Integration")]`, no test constructs a
      `FileStream` or touches the disk, and
      `dotnet build Curl.Protocol.File.UnitTests -warnaserror` is clean.

## Notes

This task adds tests. If one of them fails against the implementation, that is a finding,
not a licence to change `FileProtocolHandler` here: record it, and have `task-planner` file
it. The one exception is the strengthened chunk-sequence assertion, which is a pure
tightening of an existing test.

Depends on BL-016 and BL-017 (which change header ordering and the time-condition
comparison this task pins), and on BL-021 and BL-023 (which settle the exact failure
message text asserted above). Filing it after them is deliberate: writing these tests first
would pin behaviour those tasks then change, which is how the two retracted `FileUrlPath`
tests came about.

## Log

- 2026-09-25: Created.
- 2026-09-25: Added the four surviving mutants and the CancellingStream fake defect found by the BL-008 re-review.
