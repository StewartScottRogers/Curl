---
id: BL-101
title: Correct ITransferContext.Range's remark now that Curl.Console parses -r
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-095]
touches: [Curl.Protocol.Abstractions.UnitLibrary]
requirement: none
created: 2026-09-26
completed:
---
# BL-101 — Correct ITransferContext.Range's remark now that Curl.Console parses -r

## Goal

The `<remarks>` on `ITransferContext.Range` and `ITransferContext.MaxFileSize` in `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs` state what the code does now.

## Context

Found while finishing BL-095. The `Range` remark (around lines 45-46) ends with "`Curl.Console` does not call it yet (task BL-090)". Since BL-095, `Curl.Console/CurlCommandRunner.cs` calls `ByteRangeParser.TryParse` (in `Curl.Core.UnitLibrary`) for every transfer and passes the result into each `TransferContext`, so the sentence is false and the task reference is wrong.

The `MaxFileSize` remark says "`file://` enforces it; the other handlers do not read it yet." Check that against the protocol handlers as they are when this task runs (grep for `MaxFileSize` across `Curl.Protocol.*.UnitLibrary`) and correct it if any other handler now reads it. Also confirm the remark's other claims (Curl.Console passes `--max-filesize` in since BL-095) and add that `Curl.Console` fills it from `--max-filesize` if the remark implies otherwise.

This is a documentation-only change: no behaviour change, no rename.

## Acceptance criteria

- [ ] The `Range` remark no longer says `Curl.Console` does not call the parser and no longer cites BL-090; it states that `Curl.Console` parses `-r`/`--range` with `ByteRangeParser` before any handler runs.
- [ ] The `MaxFileSize` remark names exactly the handlers that read `MaxFileSize` in the repository at the time of the change.
- [ ] No file outside `Curl.Protocol.Abstractions.UnitLibrary` is changed.
- [ ] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
