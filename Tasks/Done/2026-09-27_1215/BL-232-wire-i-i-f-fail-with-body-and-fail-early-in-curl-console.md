---
id: BL-232
title: Wire -i, -I, -f, --fail-with-body and --fail-early in Curl.Console
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-176, BL-190, BL-231]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-232 — Wire -i, -I, -f, --fail-with-body and --fail-early in Curl.Console

## Goal

`-i`, `-I`, `-f`, `--fail-with-body` and `--fail-early` behave in `Curl.Console` as in curl 8.21.0.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W3. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- -D is wired already (BL-121).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] `-i` with `-D` and `-I` with `-o` match measured stdout, file contents and exit.
- [x] `-f` on 404 exits 22 with `curl: (22) The requested URL returned error: 404` on stderr under `-sS`/default as measured.
- [x] `--fail-early` stops the remaining URLs as measured.
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W3 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Measured 2026-09-26 with `/mingw64/bin/curl` 8.21.0 (Schannel) through `Record-CurlExchange.ps1 -Port 18332`, answering `HTTP/1.1 200 OK
Content-Length: 5

hello` or `HTTP/1.1 404 Not Found
Content-Length: 5

nope!`. Pinned one test each in `Curl.Console.UnitTests/CurlCommandRunnerHeaderAndFailTests.cs`.
  - `-sS -i -D hd.txt`: stdout head + `hello`, `hd.txt` the head only, exit 0, stderr empty.
  - `-sS -i -D -`: stdout `HTTP/1.1 200 OK
HTTP/1.1 200 OK
Content-Length: 5
Content-Length: 5


hello`: each header line goes to `-D` and to the body output before the next.
  - `-sS -i -o f`: `f` holds head + body, stdout empty. `-sS -I -o f`: request `HEAD /a HTTP/1.1`, `f` holds the head only, stdout and stderr empty, exit 0. `-sS -I`: stdout the head.
  - `-sS -f` on 404: exit 22, stderr `curl: (22) The requested URL returned error: 404`, stdout empty. `-s -f`: exit 22, nothing printed. `-f` (default): stderr is the progress meter's two header lines and the all-zero status line (exactly `ProgressMeterLines.Opening(null)`), then the `(22)` line. `-sS -i -f` on 404: stdout the head, no body, exit 22.
  - `-sS -f --fail-early` with three 404 URLs: one request, one `(22)` line, exit 22. Without `--fail-early`: every URL requested, one line each, exit 22. `--fail-early` without `-f`: a 404 is no failure, both bodies written, exit 0. `--fail-with-body --fail-early`: first body `nope!` written, one line, exit 22, one request.
- Plan (delivered in-session): `TransferContextFactory` sets `NoBody` from `-I` and chooses `HeaderOutput`: the `-D` stream alone without `-i`/`-I`; with either, the body output when there is no `-D`, otherwise the new `HeaderLineTeeStream`, which splits each write after every line feed and writes the line to the `-D` output then the body output. `HttpRequestOptionsMapping` maps `FailMode` onto `HttpRequestOptions.Fail`. `CurlCommandRunner.EndsTheRun` adds `--fail-early` (any failed transfer stops the run), and the progress meter opening is also written after an exit-22 result.
- Decisions (defaults taken from the measurements, no ADR needed as nothing was left open): the header routing rule applies to every scheme, not only HTTP, since `ITransferContext.HeaderOutput` already documents that it may be `Output` for `-i` and curl 8.21.0 prints `file://` pseudo-headers on stdout for both `-i` and `-I` (checked by hand). The meter after an exit-22 failure is the all-zero opening because `-f` reads no body; under `--fail-with-body` the real meter's live counters differ, which, like every live counter, is BL-130 to BL-132.
- Gates: `dotnet build -warnaserror` clean; `dotnet format --verify-no-changes` clean for both projects; `Measure-CodeQuality.ps1 -Library Curl.Console` (full fast run): Curl.Console.UnitTests 318 passed; Curl.Console 100% line, 100% branch, 144 members, 0 failing, worst CRAP 10. One earlier full run had two `Curl.Networking.UnitTests` failures that passed alone (281 passed) and on the rerun: load from parallel lanes, not this change (Curl.Networking is untouched).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -i, -I, -f, --fail-with-body and --fail-early behave in Curl.Console as measured on curl 8.21.0
