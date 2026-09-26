---
id: BL-022
title: Report the partial byte count on a failed transfer
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-008, BL-019]
requirement: none
created: 2026-09-25
completed:
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

- [ ] `TransferResult.Failure` takes an optional `long bytesTransferred = 0` and passes it
      through; its documentation states that a failure reports the bytes that reached the
      destination before it, not zero.
- [ ] A test in `Curl.Protocol.Abstractions.UnitTests` asserts
      `Failure(CurlExitCode.WriteError, "x", 5).BytesTransferred` is 5 and that the
      two-argument form still reports 0.
- [ ] `CopyAsync` reports its running `transferred` count on both failure paths, the read
      failure (exit 26) and the write failure (exit 23).
- [ ] A test named `ExecuteAsync_OutputFailsAfterOneChunk_ReportsTheBytesAlreadyWritten`
      downloads a 40000-byte fake file into a `FaultingStream.FailingOnWrite(2)` and
      asserts `ExitCode` is `CurlExitCode.WriteError` and `BytesTransferred` is 16384.
- [ ] A test named `ExecuteAsync_SourceFailsAfterOneChunk_ReportsTheBytesAlreadyWritten`
      uses `FaultingStream.FailingOnRead` and asserts `CurlExitCode.ReadError` with
      `BytesTransferred` 16384.
- [ ] A test asserts a failure that moved nothing — a source that cannot be opened, exit
      37 — still reports 0.
- [ ] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` and
      `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` are clean, and
      `dotnet test --filter "Category!=Integration"` is green across the solution.

## Notes

Depends on BL-019 only to keep two changes to the same record in sequence: BL-019 adds a
member to `TransferResult` and this task changes its `Failure` factory. There is no
behavioural dependency.

`--max-filesize` and its exit 63 are BL-013, not this task. This task only makes the count
truthful.

## Log

- 2026-09-25: Created.
