---
id: BL-018
title: Make FileOpenResult.LastWriteTimeUtc nullable for an unknown timestamp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-008, BL-016, BL-017]
requirement: none
created: 2026-09-25
completed:
---
# BL-018 — Make `FileOpenResult.LastWriteTimeUtc` nullable for an unknown timestamp

## Goal

`FileOpenResult.LastWriteTimeUtc` is a `DateTimeOffset?`, an unknown timestamp transfers
under `-z`/`--time-cond` instead of being read as a year-0001 file, and the
`Last-Modified` pseudo-header is omitted when there is no timestamp to put in it.

## Context

`Curl.Protocol.Abstractions.UnitLibrary\FileOpenResult.cs` declares
`DateTimeOffset LastWriteTimeUtc` and `FileOpenResult.Failed` fills it with `default`.
Two consequences, both wrong:

- An implementation that cannot stat the handle has no way to say so. It must pass
  `default(DateTimeOffset)`, which `MeetsTimeCondition` then reads as a file last written
  in the year 0001 — so an `IfModifiedSince` transfer is **skipped**. Upstream's
  `Curl_meets_timecondition` (libcurl 8.21.0) does the opposite: when the timestamp is
  unknown it transfers, because a condition that cannot be evaluated must not silently
  suppress data. See <https://curl.se/docs/manpage.html> (`-z`, `--time-cond`) and
  <https://curl.se/libcurl/c/CURLOPT_TIMECONDITION.html>.
- The same value decides whether `Last-Modified` is emitted at all. curl's `file://`
  header block carries `Last-Modified` only when the handle had a usable modification
  time; a year-0001 line is not something curl can print.

This changes the shared contract, so it touches
`Curl.Protocol.Abstractions.UnitLibrary` (`FileOpenResult`), the `file` handler
(`FileTransferMessages.PseudoHeaders` and `MeetsTimeCondition`), both
`.UnitTests` projects, and the two ADRs that describe the seam:
`ADR-0002-ifilesystem-as-the-second-protocol-seam.md` (which states that
`Content-Length` and `Last-Modified` come from the opened handle) and
`ADR-0003-itransfercontext-carries-transfer-options.md` (which lists what each protocol
must decide about the transfer options).

## Acceptance criteria

- [ ] `FileOpenResult.LastWriteTimeUtc` is `DateTimeOffset?`; `FileOpenResult.Failed`
      sets it to `null`; `FileOpenResult.Opened` accepts `DateTimeOffset?` and its
      documentation states that `null` means "the implementation could not determine a
      modification time", not "the epoch".
- [ ] A test in `Curl.Protocol.Abstractions.UnitTests\FileOpenResultTests.cs` asserts
      `Failed` returns `null` for the timestamp, and that `Opened(stream, length, null)`
      is accepted.
- [ ] `MeetsTimeCondition` transfers when the timestamp is `null`, for both
      `TimeConditionKind` values; two tests named
      `ExecuteAsync_UnknownTimestampWithIfModifiedSince_TransfersEveryByte` and
      `ExecuteAsync_UnknownTimestampWithIfUnmodifiedSince_TransfersEveryByte` assert the
      whole file reaches `Output` and the exit code is `CurlExitCode.Ok`.
- [ ] `FileTransferMessages.PseudoHeaders` takes `DateTimeOffset?` and omits the whole
      `Last-Modified` line when it is `null`, so the block is exactly
      `Content-Length: <n>\r\nAccept-ranges: bytes\r\n\r\n`; a test asserts those bytes
      verbatim, and another asserts the three-line block is unchanged when a timestamp is
      present.
- [ ] `FakeFileSystem` gains a way to add a file with no timestamp (for example
      `AddFileWithoutTimestamp`), and `FakeFileEntry` carries `DateTimeOffset?`.
- [ ] The `Last-Modified`-omission rule is recorded in the `PseudoHeaders` remarks with
      its premise: upstream libcurl 8.21.0 emits the line only for a handle with a usable
      modification time, and whether that case can be produced from the curl 8.21.0
      binary on Windows is stated either way (with the command tried, if one was).
- [ ] ADR-0002 and ADR-0003 are updated to say the timestamp is optional and what an
      absent one means for `-z` and for the header block, dated, citing curl 8.21.0.
- [ ] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` and
      `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` are clean, and
      `dotnet test --filter "Category!=Integration"` is green across the solution — the
      contract change must not leave another project failing to compile.

## Notes

Depends on BL-016 (which moves the header write) and BL-017 (which rewrites the
comparison this task adds a null case to), so the three do not fight over
`DownloadFromAsync` and `MeetsTimeCondition`.

`Curl.Core.UnitLibrary`'s `PhysicalFileSystem` does not exist yet (BL-009), so no
production implementation needs updating; only the fakes do.

## Log

- 2026-09-25: Created.
