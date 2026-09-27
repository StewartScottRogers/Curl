---
id: BL-307
title: Correct ITransferContext.MaxFileSize's remark now that HTTP enforces it
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-178]
touches: [Curl.Protocol.Abstractions.UnitLibrary]
requirement: none
created: 2026-09-26
completed:
---
# BL-307 — Correct ITransferContext.MaxFileSize's remark now that HTTP enforces it

## Goal

`ITransferContext.MaxFileSize`'s remarks say which handlers enforce `--max-filesize`, and each statement is true of the code.

## Context

- Found in BL-178. `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs` says "Only the `file://` handler enforces it; no other handler reads it yet." Since BL-178, `HttpProtocolHandler` enforces it as well: exit 63 `Maximum file size exceeded` for a Content-Length over the limit, and `Exceeded the maximum allowed file size (N) with N bytes` for a body that grows past it (ADR-0044).
- BL-178 could not make the edit because BL-292 was in Doing and touches this project.

## Acceptance criteria

- [ ] The `MaxFileSize` remarks name `file://` and `http`/`https` as the handlers that enforce it, and give both HTTP exit-63 messages.
- [ ] `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-09-26: Created.
