---
id: BL-1301
title: Stop the -w output at a variable name of 24 or more characters, as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: FR-102
created: 2026-10-02
completed:
---
# BL-1301 — Stop the -w output at a variable name of 24 or more characters, as curl does

## Goal

A `-w` template whose `%{...}` holds a name of 24 or more bytes stops writing there, silently, as curl 8.21.0 does: everything before it is written, nothing after it, and no `unknown --write-out variable` warning is printed for it.

## Context

- curl 8.21.0 `src/tool_writeout.c` (tag `curl-8_21_0`):
  - line 520: `#define MAX_WRITEOUT_NAME_LENGTH 24`; line 751: the name buffer is `curlx_dyn_init(&name, MAX_WRITEOUT_NAME_LENGTH)`;
  - lines 762-781: for `%{`, the text up to the next `}` is added with `curlx_dyn_addn`; when that fails the loop ends with `break`, so the rest of the template is never written and no warning is printed.
  - `lib/curlx/dynbuf.c` line 82: an add fails when `len + 1 > toobig`, so a 23-byte name fits and a 24-byte one does not.
- Measured on 2026-10-02 against curl 8.21.0 (Windows): `curl -s -o NUL -w 'a%{abcdefghijklmnopqrstuvw}b%{abcdefghijklmnopqrstuvwx}c\n' file:///C:/Windows/win.ini` writes `ab` to stdout (no `c`, no newline) and one line to stderr, `curl: unknown --write-out variable: 'abcdefghijklmnopqrstuvw'`; exit 0. Curl writes `abc\n` and warns for both names. A longer case, `-w '%{nosuch} %{} %{http_code %% %z \n \t \\ \x %{url.query} %{url.fragment} %{url_effective}'` against an HTTP URL, writes `  ` and warns for `nosuch` and `` only, where Curl goes on to print the fragment and URL and a third warning.
- Curl today: `Curl.Output.UnitLibrary/WriteOutTemplateRenderer.cs` `Rendering.RenderVariableAsync` reads any name up to the closing brace and `RenderTransferVariableAsync` warns for every unknown one. `StopUnlessTransferFailed` (the `%{onerror}` path) shows how the renderer already stops early by moving `position` to the end.
- The limit counts bytes of the template as curl receives it, so a name of multibyte UTF-8 characters reaches it sooner than its character count suggests. `%header{...}`, `%output{...}` and `%time{...}` have their own buffers (`HeaderNameBufferBytes` and the like) and are not part of this task.

## Acceptance criteria

- [ ] A test in `Curl.Output.UnitTests` (`WriteOutTemplateRendererTests`) renders the measured template and asserts stdout `ab`, stderr exactly `curl: unknown --write-out variable: 'abcdefghijklmnopqrstuvw'\n`, and nothing else.
- [ ] Tests pin the boundary: a known variable is unaffected, a 23-byte unknown name warns and rendering goes on, a 24-byte name stops it, and a name of twelve 2-byte UTF-8 characters (24 bytes) stops it too.
- [ ] A test pins that text pending before the stop is flushed to the current target (including after a `%{stderr}` switch), and that a `%output{}` file opened before the stop is closed as it is today at the template's end.
- [ ] `dotnet build Curl.Output.UnitTests -warnaserror` is clean; `dotnet test Curl.Output.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-03: Doing -> Blocked. Stewart: dark factory timed out after 120 min; see Z:\repos\Curl.logs\BL-1301-20261002-211047-L8.jsonl
