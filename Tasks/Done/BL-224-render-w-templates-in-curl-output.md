---
id: BL-224
title: Render -w templates in Curl.Output
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-224 — Render -w templates in Curl.Output

## Goal

A `-w` template renderer in `Curl.Output.UnitLibrary` handles variables, headers, output redirection and escapes over a variable source.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item O1. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- https://curl.se/docs/manpage.html#-w (curl 8.21.0).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] `%{var}`, `%header{name}`, `%output{file}`, `%output{>>file}`, `%{stdout}`, `%{stderr}`, `\n`, `\r`, `\t`, `\\` and `%%` render as curl does.
- [x] An unknown variable gives the measured warning line.
- [x] `dotnet build Curl.Output.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Output`.

## Notes

- Plan item: O1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered: `WriteOutTemplateRenderer.RenderAsync(template, IWriteOutVariableSource, stdout, stderr)` and `IWriteOutFileOpener` in `Curl.Output.UnitLibrary`, following curl's `src/tool_writeout.c` parse loop. `%{stdout}`/`%{stderr}` are handled by the renderer; every other `%{name}` goes to the source, which says whether curl knows it.
- Measured 2026-09-26 with curl 8.21.0 (mingw, Schannel) from a cmd script, `-s -o NUL -w "<template>" file:///C:/Windows/win.ini > out.bin 2> err.bin` (`%%` in the script is one `%`):
  - `a%%b\n|\r|\t|\\|\x|%{http_code}|%x|%{|end` -> stdout `a%b\r\n|\r|\t|\\|\x|000|%x|%{|end`. So `\\` stays two backslashes, an unknown escape or `%x` is written as is, and an unclosed `%{` is written and rendering continues.
  - `x%{nosuch}y%{` -> stdout `xy%{`, stderr `curl: unknown --write-out variable: 'nosuch'\r\n`.
  - `%{HTTP_CODE}|%{ http_code}|%{}|%` -> stdout `|||%`, three warnings: names are case-sensitive and not trimmed; a trailing `%` is literal.
  - `x%{nosuch}y%{stderr}E\n%{stdout}O%output{o3.txt}F\nG%output{>>o3.txt}H\n%{stdout}Z` -> stdout `xyOZ`, stderr warning then `E\r\n`, o3.txt `F\r\nGH\r\n`.
  - `%output{o4.txt}A%output{>>o4.txt}B%output{o4.txt}C%output{}D%output{nodir\x\y.txt}E%output{Q` -> stdout empty, o4.txt `CDE%output{Q`: an unopenable file leaves the output where it was.
  - `%output{<600 a's>}L` -> stdout `L`, no file (curl's 512-byte name buffer). `%output{o9.txt}X\n%{stderr}Y%output{o9b.txt}%{nosuch}Z` -> o9.txt `X\r\n`, stderr `Y` + warning, o9b.txt `Z`.
  - `-w Z\` -> `Z\`; `%header{content-length}|%header{x}|%header{Q` on file:// -> `||%header{Q`.
  - Against a Python loopback server sending `X-Dup: one`, `X-Dup: two`, `X-Lf: a b  `: `[%header{x-dup}][%header{X-DUP}][%header{x-lf}][%header{ x-dup}][%header{}][%header{content-length}]` -> `[one][one][a b][][][0]`: first value, case-insensitive, trimmed by the source.
- Decision: the Windows curl writes all three -w targets in text mode, so every LF, from the template or a value, becomes CR LF (measured above). The renderer takes `writesLineFeedAsCrLf` and leaves the platform choice to the Console, which knows it; the Linux/macOS builds write LF.
- Decision: text is encoded as UTF-8, and the 256-byte header-name and 512-byte file-name limits count UTF-8 bytes, like curl's C buffers.
- Decision: the renderer flushes the current file before opening the next one, because .NET's `FileMode.Append` fixes the position at open while C's append mode seeks per write; without it `%output{f}A%output{>>f}B` would overwrite A.
- Decision: no disk `IWriteOutFileOpener` here. Its tests would open real files and need `TestCategory=Integration`, which this task forbids; filed as BL-278 for Curl.Console.
- Not covered, filed as BL-279: `%time{format}` (curl 8.21.0 renders `%time{%Y}Q` as `2026Q`; today it is written literally) and `%{onerror}`. `%{json}`/`%{header_json}` are BL-227 and reach the renderer as source variables.
- Review (code-reviewer): no bugs; fixed a leak where a failing close of the previous %output file left the new one unclosed, and documented on `IWriteOutFileOpener` that two handles on one file are open at once.
- Gates: `dotnet build -warnaserror` clean; fast tests green (Curl.Output.UnitTests 23 passed; Curl.Networking.UnitTests failed 2 once under the parallel coverage run and passed on rerun, untouched here); `Measure-CodeQuality.ps1 -Library Curl.Output*` 100% line, 100% branch, 0 failing members, worst CRAP 8.

- Second run (2026-09-26): the first run's integration failed on fast tests after rebase. Cherry-picked its code commit (c18d536) onto the current base: `dotnet build -warnaserror` clean, every fast test project green (Curl.Output.UnitTests 23 passed), `Measure-CodeQuality.ps1 -Library Curl.Output*` 100% line, 100% branch, 0 failing, worst CRAP 8. The failure did not reproduce; the likely cause is the flaky Curl.Networking.UnitTests pair noted above, which this task does not touch. The follow-ups BL-256/BL-257 were never integrated, so they are re-filed as BL-279 and BL-278.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Blocked. Stewart: lane 1 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-224-lane-1-20260926-132927.
- 2026-09-26: Blocked -> Backlog. Not for Stewart: integration failed (fast tests red after rebase). The work is on branch factory/BL-224-lane-1-20260926-132927; start with git cherry-pick --no-commit factory/BL-224-lane-1-20260926-132927 and fix it.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. WriteOutTemplateRenderer renders -w variables, %header, %output, %{stdout}/%{stderr}, escapes and %% byte for byte as curl 8.21.0, with the unknown-variable warning
