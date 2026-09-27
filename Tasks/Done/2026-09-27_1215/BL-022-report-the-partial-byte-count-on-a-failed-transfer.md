---
id: BL-022
title: Report the partial byte count on a failed transfer
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-008, BL-019]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-25
completed: 2026-09-26
---
# BL-022 — Report the partial byte count on a failed transfer

## Goal

A failed transfer reports how many bytes it actually moved, so `%{size_download}` and
`%{size_upload}` can match curl 8.21.0 once `--write-out` exists.

## Context

Measured on this machine against curl 8.21.0 (Release-Date 2026-06-24): a transfer that
wrote 5 bytes and then failed with exit 63 (`CURLE_FILESIZE_EXCEEDED`, `--max-filesize`)
still reports `%{size_download}=5`. A failure does not reset the counter; the bytes that
reached the destination are reported whatever the outcome
(<https://curl.se/docs/manpage.html>, `--write-out`;
<https://curl.se/libcurl/c/libcurl-errors.html>).

`Curl.Protocol.Abstractions.UnitLibrary\TransferResult.cs` makes that impossible to report:

```csharp
public static TransferResult Failure(CurlExitCode exitCode, string errorMessage) =>
    new(exitCode, 0, errorMessage);
```

`FileProtocolHandler.CopyAsync` knows the count — it keeps `transferred` — and throws it
away on every failure path. Severity is Minor because nothing consumes
`BytesTransferred` yet; it matters when `--write-out` lands, and `--max-filesize` (BL-013)
is the first failure that will be measured against it.

This changes `Curl.Protocol.Abstractions.UnitLibrary`, so it touches the abstractions, the
`file` handler and both `.UnitTests` projects.

## Acceptance criteria

- [x] `TransferResult.Failure` takes an optional `long bytesTransferred = 0` and passes it
      through; its documentation states that a failure reports the bytes that reached the
      destination before it, not zero.
- [x] A test in `Curl.Protocol.Abstractions.UnitTests` asserts
      `Failure(CurlExitCode.WriteError, "x", 5).BytesTransferred` is 5 and that the
      two-argument form still reports 0.
- [x] `CopyAsync` reports its running `transferred` count on both failure paths, the read
      failure (exit 26) and the write failure (exit 23).
- [x] A test named `ExecuteAsync_OutputFailsAfterOneChunk_ReportsTheBytesAlreadyWritten`
      downloads a 40000-byte fake file into a `FaultingStream.FailingOnWrite(2)` and
      asserts `ExitCode` is `CurlExitCode.WriteError` and `BytesTransferred` is 16384.
- [x] A test named `ExecuteAsync_SourceFailsAfterOneChunk_ReportsTheBytesAlreadyWritten`
      uses `FaultingStream.FailingOnRead` and asserts `CurlExitCode.ReadError` with
      `BytesTransferred` 16384.
- [x] A test asserts a failure that moved nothing — a source that cannot be opened, exit
      37 — still reports 0.
- [x] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` and
      `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` are clean, and
      `dotnet test --filter "Category!=Integration"` is green across the solution.

## Notes

Depends on BL-019 only to keep two changes to the same record in sequence: BL-019 adds a
member to `TransferResult` and this task changes its `Failure` factory. There is no
behavioural dependency.

`--max-filesize` and its exit 63 are BL-013, not this task. This task only makes the count
truthful.

Delivered directly rather than through the full `/feature` agent chain: the change is one
optional parameter and one lambda, and the plan needed no architecture. What was found:

- Since this task was filed, the upload paths (exit 26 read failure, exit 55 send failure)
  and exit 63 already build `TransferResult` with the running count; the only path that
  still dropped it was the download write failure (exit 23), which now passes it through.
- A download read failure is no longer exit 26: curl 8.21.0 ends the body there and exits
  0 (`ExecuteAsync_SourceReadFailsMidBody_EndsTheBodyThereAndSucceeds`). So
  `ExecuteAsync_SourceFailsAfterOneChunk_ReportsTheBytesAlreadyWritten` drives the exit 26
  path that exists, an upload of known length whose second read fails. Chosen because it
  is the only read failure that still returns `CurlExitCode.ReadError`.
- `Requirements.md` FR-018 now states what is implemented; its status is left `Draft`
  because nothing consumes the count until `--write-out` lands.
- The last criterion's `Category!=Integration` filter is run as `TestCategory!=Integration`,
  the MSTest spelling CLAUDE.md uses.

## Log

- 2026-09-25: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. A failed file:// transfer reports the bytes that reached the destination, not zero
