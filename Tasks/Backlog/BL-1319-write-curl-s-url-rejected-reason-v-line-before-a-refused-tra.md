---
id: BL-1319
title: Write curl's '* URL rejected: <reason>' -v line before a refused transfer URL's exit 3
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1319 — Write curl's '* URL rejected: <reason>' -v line before a refused transfer URL's exit 3

## Goal

Under `-v`, a transfer URL curl's parser rejects writes curl 8.21.0's info line `* URL rejected: <reason>` to standard error before the `curl: (3) URL rejected: <reason>` (or `curl: (67) ...`) line, and `-sv` writes the info line alone.

## Context

- Today `Curl.Console/CurlCommandRunner.cs` turns a rejected URL into `UrlRejectedFailure(CurlUrlRejection)` (exit 3, or exit 67 for `CurlUrlRejection.UserNotAllowed`, message `UrlRejectedPrefix + rejection.ToCurlMessage()`) and returns it from the transfer path that calls `TryParseTransferUrl`. Nothing is reported to the transfer's events, so `-v` writes no `*` line for it.
- curl 8.21.0, `lib/url.c` line 2255 (tag `curl-8_21_0`): `failf(data, "URL rejected: %s", curl_url_strerror(uc));`. `failf` writes its text as a `-v` info line as well as keeping it for the `curl: (N)` line.
- Measured 2026-10-03 with the installed curl 8.21.0 (mingw, Schannel), stderr bytes:
  - `curl -v "http://h/a b"`: `* URL rejected: Malformed input to a URL function\r\n` then `curl: (3) URL rejected: Malformed input to a URL function\r\n`, exit 3.
  - `curl -sv "http://h/a b"`: only `* URL rejected: Malformed input to a URL function\r\n`, exit 3.
  - `curl -v "http://127.0.0.1:99999/"`: `* URL rejected: Port number was not a decimal number between 0 and 65535`, then the `curl: (3)` line.
  - `curl -v --disallow-username-in-url "http://u@127.0.0.1/"`: `* URL rejected: Credentials was passed in the URL when prohibited`, then `curl: (67) URL rejected: ...`.
  - `curl -v "file://host/x"`: `* URL rejected: Bad file:// URL`, then the `curl: (3)` line.
- Report the line the way the runner reports its other pre-connection info lines (`events.ReportInfo(...)`, e.g. `InterfaceSetoptMessage` near line 2816), so `--trace`/`--trace-ascii` get it as their `* ` info line too. Line endings on standard error follow the existing writer (CR LF on Windows), as the other `*` lines do.
- Existing tests that pin the `curl: (3) URL rejected:` line without `-v` (`CurlCommandRunnerTests`, `CurlCommandRunnerDisallowUsernameInUrlTests`) must still pass unchanged: without `-v` no `*` line is written.

## Acceptance criteria

- [ ] A test in `Curl.Console.UnitTests` runs `-v "http://h/a b"` and asserts standard error is exactly `* URL rejected: Malformed input to a URL function` + newline + `curl: (3) URL rejected: Malformed input to a URL function` + newline, exit 3.
- [ ] A test runs `-sv "http://h/a b"` and asserts standard error is only the `* URL rejected: ...` line, exit 3.
- [ ] A data-driven test pins the `* URL rejected:` line for the bad port (`http://127.0.0.1:99999/`), `file://host/x` (`Bad file:// URL`), and `--disallow-username-in-url http://u@127.0.0.1/` (exit 67, `Credentials was passed in the URL when prohibited`) cases, each URL drive-less so the test passes on Windows, Linux and macOS.
- [ ] A test runs `--trace-ascii - "http://h/a b"` and asserts standard output is exactly `* URL rejected: Malformed input to a URL function\n` (LF, as measured 2026-10-03: curl 8.21.0 wrote those 50 bytes to stdout and the `curl: (3)` line to stderr).
- [ ] Every existing `URL rejected` test passes unchanged.
- [ ] `dotnet build Curl.Console.UnitTests -warnaserror` is clean; `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` reports no failing member.

## Notes

## Log

- 2026-10-03: Created.
