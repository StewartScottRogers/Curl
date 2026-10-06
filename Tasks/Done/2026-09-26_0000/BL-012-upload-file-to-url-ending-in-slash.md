---
id: BL-012
title: Handle -T/--upload-file to a URL ending in /
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
requirement: none
created: 2026-09-25
completed: 2026-09-26
---
# BL-012 — Handle `-T`/`--upload-file` to a URL ending in `/`

## Goal

When `-T`/`--upload-file` targets a URL ending in `/`, the local file's base name is
appended to the URL before the transfer is dispatched.

## Context

This is a command-line-layer concern in `Curl.Cli.UnitLibrary`. It is resolved before
`ITransferContext` is constructed, so no protocol handler ever sees an unresolved
trailing-slash URL. Upstream behaviour: https://curl.se/docs/manpage.html,
`--upload-file`.

## Acceptance criteria

- [x] `-T local.txt ftp://host/dir/` produces the URL `ftp://host/dir/local.txt`
      before the transfer starts.
- [x] A URL not ending in `/` is left unchanged.
- [x] The resolution lives in `Curl.Cli.UnitLibrary`; no protocol handler changes.

## Notes

- Plan (protocol-architect, checked against curl 8.21.0 `src/tool_operhlp.c`
  `add_file_name_to_url` and `stdin_upload`): a pure static `Curl.Cli.UploadUrl` with
  `IsStandardInput` and `AppendLocalFileNameWhenUrlNamesNoFile`. The URL is split
  textually; validation and normalisation stay with the URL layer (BL-010).
- Choice (unattended run): follow upstream where it is broader than criterion 2. A URL
  with no path (`ftp://host`) gains `/local.txt`, a fragment is kept after the appended
  name (`/dir/#f` -> `/dir/local.txt#f`), a non-empty query leaves even a `/`-ending URL
  unchanged, and an empty `?` is dropped. Why: the product is a drop-in replacement, so
  matching curl is not a divergence and needs no decision from Stewart. Criterion 2 is
  read as "a URL whose path names a file is left unchanged".
- Choice: the base name is percent-encoded as UTF-8 (`Uri.EscapeDataString`, the same
  unreserved set as `curl_easy_escape`). Why: Linux, macOS and Unicode Windows builds of
  curl pass UTF-8; only non-Unicode Windows builds encode the ANSI code-page byte.
- `-T -` and `-T .` (standard input) leave the URL unchanged. The local file is never
  opened here; a missing file still gets its name appended and fails later with 26.
- Nothing calls `UploadUrl` yet: there is no option parser or transfer dispatcher.
  Wiring it in, and `-T` globbing, are follow-up tasks.
- Review and conformance: curl's scheme guess accepts `scheme:` followed by one to three
  slashes, so `http:/host` and `http:///host` also get the name appended; fixed and
  pinned by tests. The UTF-8 choice is recorded in ADR-0004 (Proposed, for Stewart to
  accept); it matches the Microsoft Windows build and UTF-8 Linux and macOS builds.
- Verified: `UploadUrlTests` has 58 tests, 100% line and branch coverage of `UploadUrl`;
  criterion 1 by `AppendLocalFileNameWhenUrlNamesNoFile_PathEndsInSlash_AppendsFileName`,
  criterion 2 by `..._PathNamesAFile_ReturnsUrlUnchanged`, criterion 3 by the diff (no
  protocol library touched).
- Follow-ups filed: BL-030 (wire `UploadUrl` into the transfer dispatcher, with exit 3
  for a malformed URL at that point) and BL-031 (globs in the `-T` argument).

## Log

- 2026-09-25: Migrated from Documentation/Planning/Backlog.md (Ready).
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. UploadUrl appends the -T file's percent-encoded base name to a URL naming no file, as curl 8.21.0 does
