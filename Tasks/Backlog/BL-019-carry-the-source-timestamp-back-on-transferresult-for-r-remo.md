---
id: BL-019
title: Carry the source timestamp back on TransferResult for -R/--remote-time
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-008, BL-018]
requirement: none
created: 2026-09-25
completed:
---
# BL-019 — Carry the source timestamp back on `TransferResult` for `-R`/`--remote-time`

## Goal

`TransferResult` carries the source's modification time, truncated to whole seconds, so
the caller that owns the output file can apply `-R`/`--remote-time` to it.

## Context

Measured on this machine against curl 8.21.0 (Release-Date 2026-06-24): `-R` applies to
`file://`. `curl -R -o out.txt file:///C:/dir/hello.txt` leaves `out.txt` with the
source's modification time, truncated to whole seconds. `curl --help all` documents it as
`-R, --remote-time  Set remote file's time on local output`
(<https://curl.se/docs/manpage.html>).

`FileProtocolHandler` already holds the value it needs —
`FileOpenResult.LastWriteTimeUtc` from the read open — but has nowhere to put it:
`Curl.Protocol.Abstractions.UnitLibrary\TransferResult.cs` is
`(CurlExitCode ExitCode, long BytesTransferred, string? ErrorMessage)`. The handler cannot
set the timestamp itself either: its destination is `ITransferContext.Output`, a `Stream`,
and it does not know or own the file behind it. So the timestamp travels back on the
result and whoever opened `Output` applies it.

This changes the shared contract, so it touches
`Curl.Protocol.Abstractions.UnitLibrary`, its tests, the `file` handler and its tests, and
ADR-0003, which is the record of how transfer options are carried.

## Acceptance criteria

- [ ] `TransferResult` gains `DateTimeOffset? SourceLastWriteTimeUtc` as its last
      positional member, defaulting to `null`, documented as "the modification time of
      the resource that was read, for the caller to apply when `-R`/`--remote-time` was
      asked for; `null` when it is unknown or no source was opened". Every existing
      construction of `TransferResult` still compiles.
- [ ] `TransferResult.Success` gains an optional `DateTimeOffset?` parameter;
      `TransferResult.Failure` leaves the timestamp `null`.
- [ ] A test in `Curl.Protocol.Abstractions.UnitTests` asserts `Success(10)` leaves
      `SourceLastWriteTimeUtc` `null` and `Success(10, someTime)` carries it.
- [ ] On a successful download, `FileProtocolHandler` sets `SourceLastWriteTimeUtc` from
      `FileOpenResult.LastWriteTimeUtc` truncated to whole seconds; a test named
      `ExecuteAsync_Download_ReportsTheSourceTimestampTruncatedToSeconds` uses a fake file
      timestamp with 750 milliseconds and asserts the reported value has zero
      sub-second ticks and equals the truncated timestamp.
- [ ] A download whose source timestamp is unknown (`null`, per BL-018) reports `null`; a
      test asserts it.
- [ ] An upload reports `null` — there is no remote source to take a time from — and a
      test named `ExecuteAsync_Upload_ReportsNoSourceTimestamp` asserts it.
- [ ] A test asserts a `-I`/`NoBody` download still reports the timestamp: `-R` with
      `-I` writes no body but the metadata is still known.
- [ ] ADR-0003 records that the result, not only the context, carries transfer-option
      data, with the reason (`file://` cannot apply `-R` itself because it does not own
      `Output`), dated, citing curl 8.21.0.
- [ ] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` and
      `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` are clean, and
      `dotnet test --filter "Category!=Integration"` is green across the solution.

## Notes

Applying the timestamp to a real output file is not in this task: no code owns the output
file yet, and `PhysicalFileSystem` (BL-009) plus the command line layer are where `-R`
finally lands. This task makes the value reachable and pins its shape.

Depends on BL-018 because the value it forwards becomes nullable there; doing it the other
way round would mean changing the same two files twice.

## Log

- 2026-09-25: Created.
