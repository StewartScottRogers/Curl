---
id: BL-025
title: State the file:// upload path's ignored options and its lack of path sandboxing
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-008, BL-020]
touches: [Curl.Protocol.File.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-09-25
completed: 2026-09-26
---
# BL-025 — State the `file://` upload path's ignored options and its lack of path sandboxing

## Goal

`Curl.Protocol.File.UnitLibrary\CLAUDE.md` states, per transfer option, what the `file`
handler does with it on each direction, and states that no layer sandboxes a path between
the URL and the disk.

## Context

ADR-0003
(`Documentation\Planning\Decisions\ADR-0003-itransfercontext-carries-transfer-options.md`)
requires each protocol to say which of the `ITransferContext` transfer options it ignores:
"'ignored' is a legitimate, expected answer here too — but it must be stated per protocol
(in that protocol's own documentation or tests), not left implicit."

For `file` that statement is incomplete. The `FileProtocolHandler` class remarks name only
`TimeProvider` as deliberately unused, while on the **upload** path `Range`, `NoBody`,
`TimeCondition` and `HeaderOutput` are all silently ignored: `UploadAsync` reads
`ResumeFrom` and nothing else. A reader of the handler today cannot tell whether that is a
decision or an omission.

The second statement is about trust, not conformance. `FileUrlPath` deliberately hands on
paths the operating system may reject or may resolve somewhere surprising, and after BL-015
it resolves `..` the way curl does rather than confining anything. Nothing between the URL
and `IFileSystem` restricts which paths are reachable. That is **correct** for a curl
clone — `curl file:///C:/Windows/win.ini` is meant to work — but it is exactly the kind of
property a future caller embedding this library would assume the opposite of, so the
library's own `CLAUDE.md` should say it out loud.

## Acceptance criteria

- [x] `Curl.Protocol.File.UnitLibrary\CLAUDE.md` gains a table with one row per
      `ITransferContext` member — `ResumeFrom`, `Range`, `NoBody`, `TimeCondition`,
      `HeaderOutput`, `ConvertLineEndings`, `TimeProvider` — and a column each for
      download and upload, saying "honoured" or "ignored" for each, matching the code as it
      stands when the task is done.
- [x] The same file states that `TimeProvider` is unused in both directions and why
      (nothing in a local file transfer is timed or retried, and `-z` compares against the
      timestamp the open reported).
- [x] The same file states, in its own short section, that no component between the URL and
      `IFileSystem` sandboxes or confines a path; that this matches curl, with a link to
      <https://curl.se/docs/url-syntax.html> and the curl version checked (8.21.0); and
      that an `IFileSystem` implementation must not treat a path it receives as validated
      or trusted.
- [x] Every claim in the table is verified against
      `Curl.Protocol.File.UnitLibrary\FileProtocolHandler.cs` at the time of writing, and
      the section names the methods it read (`DownloadFromAsync`, `UploadAsync`,
      `UploadIntoAsync`, `TryResolveWindow`).
- [x] No `.cs` file is changed by this task, and `Curl.Protocol.File.UnitLibrary\CLAUDE.md`
      repeats no rule already in the root `CLAUDE.md`.

## Notes

The XML remarks on the `FileProtocolHandler` class are out of scope: this task is scoped to
Markdown (see the last acceptance criterion). Whoever next edits that file should bring the
class remarks in line with the table this task writes.

Depends on BL-020, which adds `ConvertLineEndings` to `ITransferContext` and makes the
upload path honour it; writing the table before that lands would make it wrong on its first
day.

Delivered by the session directly rather than through `align-and-document`: one Markdown
file, and the work is reading `FileProtocolHandler.cs` against the table.

Choices made unattended:
- The table also has rows for `MaxFileSize` (download honoured, upload ignored),
  `CreateFileMode` (download ignored, upload honoured) and one row for the protocol-specific
  members (`PostData`, `Credentials`, `TelnetOptions`, `TftpBlockSize`, `TftpNoOptions`,
  ignored both ways). The criterion names seven members; ADR-0003 asks for every ignored
  option, so leaving the rest implicit would have repeated the gap this task closes.
  `Url`, `Output`, `Upload` and `CancellationToken` are not transfer options and have no row.
- The curl claim cites url-syntax.html's two statements that bear on it: only the hostname
  of a `file://` URL is restricted, and on Windows a path may turn into an SMB access curl
  cannot control.

Found while reading: the `ExecuteAsync` remarks say "below this point the two paths share
nothing", but both call `CopyAsync`. Out of scope here (no `.cs` changes); worth fixing with
the class-remarks alignment noted above, filed as BL-107.

## Log

- 2026-09-25: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. File CLAUDE.md states each transfer option per direction and that no layer sandboxes a path
