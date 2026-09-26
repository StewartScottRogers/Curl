---
id: BL-079
title: Apply -R/--remote-time to the output file from TransferResult.SourceLastWriteTimeUtc
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-019, BL-009, BL-068]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console]
requirement: FR-011
created: 2026-09-26
completed:
---
# BL-079 — Apply -R/--remote-time to the output file from TransferResult.SourceLastWriteTimeUtc

## Goal

When `-R`/`--remote-time` is given and the transfer succeeds, the output file's
modification time is set to `TransferResult.SourceLastWriteTimeUtc`.

## Context

BL-019 made the value reachable: `TransferResult.SourceLastWriteTimeUtc`
(`Curl.Protocol.Abstractions.UnitLibrary/TransferResult.cs`) carries the source's
modification time in whole seconds, `null` when unknown or on an upload. A handler
cannot apply it because it does not own `ITransferContext.Output`
(ADR-0003, amendment of 2026-09-26). Whoever opens the `-o` output file applies it.
`PhysicalFileSystem` (BL-009, `Curl.Core.UnitLibrary/FileSystem`) is the real-disk
file system. curl 8.21.0: `curl -R -o out.txt file:///C:/dir/hello.txt` leaves `out.txt`
with the source's modification time truncated to whole seconds
(<https://curl.se/docs/manpage.html#-R>). Check upstream for the stdout case (no file,
nothing applied) and a `-z` condition that is not met before pinning them.

## Acceptance criteria

- [ ] With `-R` and a successful transfer whose `SourceLastWriteTimeUtc` is not `null`,
      the output file's last-write time equals that value; a test asserts it.
- [ ] Without `-R`, the output file's last-write time is left alone; a test asserts it.
- [ ] A `null` `SourceLastWriteTimeUtc` or a failed transfer leaves the output file's
      time alone; a test asserts each.
- [ ] Output to stdout under `-R` sets nothing and does not fail.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

Filed by BL-019 as its follow-up. `touches` is a best guess at where the output file is
opened; re-plan it if the command line layer puts that elsewhere.

## Log

- 2026-09-26: Created.
