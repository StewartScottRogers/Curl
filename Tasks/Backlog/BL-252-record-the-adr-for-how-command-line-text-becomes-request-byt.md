---
id: BL-252
title: Record the ADR for how command-line text becomes request bytes on each platform
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-09-26
completed:
---
# BL-252 — Record the ADR for how command-line text becomes request bytes on each platform

## Goal

An ADR under `Documentation/Planning/Decisions/` decides, per platform, how command-line text (such as `-H` values) becomes request bytes, and which layer owns that conversion.

## Context

- Found while doing BL-172 (the HTTP request head formatter, `Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs`).
- Measured in BL-172: curl 8.21.0 on Windows (mingw build, the Windows HTTP reference per `Documentation/Planning/Decisions/ADR-0018-the-mingw-curl-8-21-0-build-is-the-windows-http-reference.md`) sends `-H` text through the ANSI code page with best fit:
  - `é` (U+00E9) -> `0xE9`
  - `Ā` (U+0100) -> `A` (`0x41`)
  - `€` (U+20AC) -> `0x80`
  - `中` (U+4E2D) -> `?` (`0x3F`)
- `HttpRequestHeadFormatter` today encodes with .NET `Encoding.Latin1`, which matches `é`, U+0100 and `中` but sends `?` for `€`, because cp1252's 0x80-0x9F extras differ from Latin-1.
- curl on Linux and macOS passes argv bytes through unchanged, normally UTF-8.
- Options to weigh on Windows: the active code page with best fit vs Latin-1. Linux/macOS: UTF-8 (argv bytes). The decision must stay base-class-library only (root `CLAUDE.md`). Note in the ADR that the `System.Text.Encoding.CodePages` provider ships in the shared framework (no package needed), but .NET's code-page encoders do not perform Windows best-fit mapping, so an exact match for `Ā` -> `A` needs either a hand-rolled best-fit table or a P/Invoke to `WideCharToMultiByte` (check that it stays native-AOT compatible).
- The ADR also decides which layer owns the conversion: `Curl.Cli.UnitLibrary` (argument text to bytes) or the HTTP formatter in `Curl.Protocol.Http.UnitLibrary`, and states what type crosses the boundary.
- Related: `Documentation/Planning/Decisions/ADR-0004-upload-file-name-percent-encoded-as-utf8.md` (an earlier text-to-bytes decision; say whether it is consistent).

## Acceptance criteria

- [ ] A new ADR file exists under `Documentation/Planning/Decisions/`, numbered after the highest existing ADR, following the format of the existing ADRs.
- [ ] The ADR states the per-platform decision (Windows, Linux, macOS) and which layer (`Curl.Cli` or the HTTP formatter) owns the conversion.
- [ ] The ADR records the four measured Windows bytes above (`é` -> `0xE9`, U+0100 -> `A`, `€` -> `0x80`, `中` -> `?`) with the curl version and build (8.21.0, mingw).
- [ ] The ADR says the decision stays base-class-library only and addresses the CodePages provider / best-fit point.
- [ ] The ADR contains the line "Decided by Claude under Stewart's delegation".
- [ ] `Documentation/Planning/Decisions/README.md` lists the new ADR if that file indexes ADRs.
- [ ] If the decision changes code, a follow-up implementation task is filed in `Tasks/Backlog` with the task-board script, and its ID is named in this task's Notes; if not, Notes says why no code change is needed.

## Notes

## Log

- 2026-09-26: Created.
