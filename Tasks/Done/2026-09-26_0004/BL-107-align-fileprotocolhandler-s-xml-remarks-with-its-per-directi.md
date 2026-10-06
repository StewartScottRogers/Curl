---
id: BL-107
title: Align FileProtocolHandler's XML remarks with its per-direction options table
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-025]
touches: [Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-107 — Align FileProtocolHandler's XML remarks with its per-direction options table

## Goal

The XML remarks in `Curl.Protocol.File.UnitLibrary\FileProtocolHandler.cs` say the same
thing about each `ITransferContext` option as the per-direction table in
`Curl.Protocol.File.UnitLibrary\CLAUDE.md`, and nothing false about the two paths.

## Context

BL-025 wrote the table in `CLAUDE.md` ("Transfer options, per direction"). Two remarks in
`FileProtocolHandler.cs` lag it:

- The class remarks name only `TimeProvider` as unused, while the upload path also ignores
  `Range`, `NoBody`, `TimeCondition`, `HeaderOutput` and `MaxFileSize`, and the download
  path ignores `ConvertLineEndings` and `CreateFileMode`.
- The `ExecuteAsync` remarks say that below the negative-resume check "the two paths share
  nothing", but `DownloadFromAsync` and `UploadIntoAsync` both call `CopyAsync`.

## Acceptance criteria

- [x] The `FileProtocolHandler` class remarks name every option the upload path ignores and
      every option the download path ignores, matching the `CLAUDE.md` table, or point to
      that table by path instead of listing them.
- [x] The `ExecuteAsync` remarks no longer claim the two paths share nothing.
- [x] Only XML comments change: no behaviour change, `dotnet build` clean and the fast tests
      green.

## Notes

- Done in-session rather than through `align-and-document`: two XML comment paragraphs
  in one file. The class remarks now list the ignored options per direction and point to
  the `CLAUDE.md` table by path for the full list (including the other protocols' members).
- `ExecuteAsync` remarks now say the paths diverge below the negative-resume check and meet
  again only in the shared chunked copy (`CopyAsync`).
- Verified: `dotnet build` 0 errors; fast tests all passed (File.UnitTests 199/199).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. FileProtocolHandler's XML remarks now match the per-direction options table and no longer claim the two paths share nothing
