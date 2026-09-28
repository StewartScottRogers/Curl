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
completed: 2026-09-27
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

- [x] A new ADR file exists under `Documentation/Planning/Decisions/`, numbered after the highest existing ADR, following the format of the existing ADRs.
- [x] The ADR states the per-platform decision (Windows, Linux, macOS) and which layer (`Curl.Cli` or the HTTP formatter) owns the conversion.
- [x] The ADR records the four measured Windows bytes above (`é` -> `0xE9`, U+0100 -> `A`, `€` -> `0x80`, `中` -> `?`) with the curl version and build (8.21.0, mingw).
- [x] The ADR says the decision stays base-class-library only and addresses the CodePages provider / best-fit point.
- [x] The ADR contains the line "Decided by Claude under Stewart's delegation".
- [x] `Documentation/Planning/Decisions/README.md` lists the new ADR if that file indexes ADRs.
- [x] If the decision changes code, a follow-up implementation task is filed in `Tasks/Backlog` with the task-board script, and its ID is named in this task's Notes; if not, Notes says why no code change is needed.

## Notes

- ADR-0067 records the decision: Windows uses the system ANSI code page with best fit, Linux and macOS use UTF-8. This is the encoding `CredentialEncoding.ForPlatform` already gives credentials (ADR-0022). The HTTP formatter owns the conversion. `string` crosses the boundary, and `Curl.Console` sets the `Encoding` on `HttpRequestOptions`.
- The Context said .NET's code-page encoders do not best-fit. The repository shows they do for Windows-1252: `BasicAndBearerAuthenticatorTests` pins `-u Ω中Ā:p` to `Basic Tz9BOnA=` (`O?A`) through `CodePagesEncodingProvider`. So no hand-rolled table and no `WideCharToMultiByte` P/Invoke are needed. The ADR says so.
- The decision changes code. Follow-up implementation task: BL-373 (feature). It touches Abstractions, Http, Console and their tests.
- The ADR notes that ADR-0004 and ADR-0064 (UTF-8 for the `-T` file name and for `--variable`) do not agree with it on Windows. Neither is superseded, because the URL and option expansion are outside this ADR's scope. Request bodies (`-d`, `-F`) are also left undecided.
- The ADR was not re-measured against curl, because no output text is pinned here. It relies on the four Windows bytes BL-172 measured.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ADR-0067 decides per-platform encoding of command-line header text and that the HTTP formatter owns it; implementation filed as BL-373
