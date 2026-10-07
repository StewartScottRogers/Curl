---
id: BL-235
title: Print -w output after each transfer in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-225, BL-226, BL-194, BL-231]
touches: [Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-235 — Print -w output after each transfer in Curl.Console

## Goal

`-w` renders after each transfer to stdout or stderr as the template says, also after a failure.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W6. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] `-w '%{http_code}\n'` after success and after a failure matches curl 8.21.0 (measured), including stream choice.
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W6 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0040; no task in Doing named it.
- Delivered: `CurlCommandRunner` renders `-w` with `WriteOutTemplateRenderer` and `TransferWriteOutVariables` after each transfer's failure lines, success or failure (also after a `-D` or resumed `-o` open failure). New `LineFeedToCrLfStream` (Windows text mode) and `RefusingWriteOutFileOpener` (default until BL-280). New runner parameters `writeOutFileOpener` and `timeProvider` (default `TimeProvider.System`, the provider the transports use).
- Measured 2026-09-26 with curl 8.21.0 (`/mingw64/bin/curl`), `Record-CurlExchange.ps1` on 127.0.0.1 (200 `hello`, 404 `gone`) or `file://` URLs; pinned in `CurlCommandRunnerWriteOutTests`:
  - `-w '%{http_code}\n' URL` (200) -> stdout `hello200\n` (LF: stdout is binary once a body goes there), exit 0.
  - `-s -f -w '%{http_code}\n' URL` (404) -> stdout `404\n`, stderr empty, exit 22. Without `-s`: meter, then `curl: (22) The requested URL returned error: 404\r\n` on stderr.
  - `-sS -f -w '%{stderr}%{http_code}\n' URL` (404) -> stderr `curl: (22) The requested URL returned error: 404\r\n404\r\n`: -w after the failure line, stderr CR LF.
  - `-s -o NUL -w ...` -> `200\r\n`; `-s -f -o NUL` (404) -> `404\r\n`: stdout stays text mode when no body goes there.
  - Several URLs, `W = -w "%{exitcode}\n"`: `-o NUL -o NUL W f f g` -> `0\n0\n` + g body + `0\n`; `-o NUL W <refused> g` -> `7\n` + body + `0\n`; `--fail-early -o NUL -o NUL W <refused> f g` -> `7\r\n`; `-D <dir> W f` -> `23\r\n`; `-o NUL -C 3 -o <dir> W f f g` -> `0\r\n23\r\n`. Explained by the stdio buffer being translated at flush time; ADR-0040.
  - Failures still render: malformed `dict://exa mple.com/d:x` -> `000|3`, `-F a=@nosuchfile` -> `000|26`, `-r x` -> `000|33`, `-D <dir>` and `-C 3 -o <dir>` -> `000|23`.
  - `%{scheme}` is empty for `xyz://a/b` and the malformed URL; `%{url}` is as typed; `%{urlnum}` counts from 0.
  - Closed stdout, 10000-byte `file://` body, `-sS -w 'A%{exitcode}%{stderr}B%{exitcode}\n'` -> stderr `curl: (23) ...\r\nB23\r\n`: the stdout part is lost, the rest renders.
- Decision (ADR-0040): renderer runs with `writesLineFeedAsCrLf: false`; the runner wraps stderr (Windows) and stdout (Windows, while text mode) in `LineFeedToCrLfStream`. Known unmodelled case: a later resume-open failure ending the run before a stdout URL (curl CR LF, ours LF).
- Choice: `-w` stdout goes to `Stream.Null` once standard output has failed, so a closed stdout cannot throw out of the runner (found by code-reviewer).
- Not matched, left to other tasks: `%{url_effective}` is the URL as typed plus the `-G` query; curl normalizes it (`FILE:///C:/x` -> `file://C:/x`), which needs the CurlUrl parser (BL-292). `%output{file}` opens nothing until BL-280 (its Context now says to wrap files in `LineFeedToCrLfStream`).
- Gates: `dotnet build -warnaserror` clean; fast tests green in all 16 test projects (Curl.Console.UnitTests 386); `Measure-CodeQuality.ps1 -Library Curl.Console` 100% line, 100% branch, 0 failing, worst CRAP 10; `dotnet format --verify-no-changes` clean. The built `curl.exe` gave byte-identical stdout/stderr to curl 8.21.0 for the 200, 404-to-stderr and 404 `-o NUL` exchanges.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -w renders after every transfer, success or failure, to stdout or stderr with curl 8.21.0's Windows line feeds
