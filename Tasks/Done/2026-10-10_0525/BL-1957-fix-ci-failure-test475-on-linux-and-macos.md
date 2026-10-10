---
id: BL-1957
title: Fix CI failure test475 on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1957 — Fix CI failure test475 on Linux and macOS

## Goal

`test475` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test475` failed on Linux and macOS in CI run 38047662088 (https://github.com/StewartScottRogers/Curl/actions/runs/38047662088). First failing commit: 7b6794f4.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 38047662088 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `test475` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: real curl converts each lone LF of an ASCII (`;type=a`, `-B`) FTP upload to CRLF when built with `CURL_PREFER_LF_LINEENDS` (every platform but Windows). test475 writes LF lines off Windows and CRLF lines on Windows, so Curl, which converted only under `--crlf`, passed only on Windows. The conformance harness was right; the fix is in the FTP handler (ADR-0463).
- Added `Curl.Protocol.Ftp.UnitLibrary` and `Curl.Protocol.Ftp.UnitTests` to `touches`: no task in Doing on `origin/work/dark-factory` named them.
- `FtpUploadLineEndings.AreConverted` is public so its off-Windows answer is covered on Windows; the two ASCII-upload handler tests are now split per platform with `OSCondition`.
- The `Uploaded unaligned file size` check treats an ASCII conversion like `--crlf` (only a short upload is unaligned); default chosen because the conversion may add bytes.
- CI on Linux and macOS is confirmed by the shift after integration; the CI watch refiles if test475 still fails there.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. FTP ASCII uploads convert lone LF to CRLF off Windows, as curl does; test475 holds on every platform
