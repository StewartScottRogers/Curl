---
id: BL-1298
title: Keep the header's line ending in curl's skipped cookie with bad tailmatch domain line
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-03
---
# BL-1298 — Keep the header's line ending in curl's skipped cookie with bad tailmatch domain line

## Goal

The `-v` refusal Curl writes for a `Set-Cookie` whose `Domain` the host may not set ends, like curl 8.21.0's, with the rest of the header line including its CR LF, so the line is followed by an empty line exactly as curl's is.

## Context

- curl 8.21.0 `lib/cookie.c` (tag `curl-8_21_0`) lines 518-525: `infof(data, "skipped cookie with bad tailmatch domain: %s", curlx_str(val))`. `curlx_str` is the start of the `Domain` value inside the header line and is not NUL-terminated at the value's end, so `%s` prints everything to the end of the header line as curl received it, its `\r\n` included; `infof` then adds its own `\n`.
- Measured on 2026-10-02 against curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1` answering `HTTP/1.1 200 OK\r\nContent-Length: 0\r\nSet-Cookie: g=1; Domain=.example.com; Path=/\r\nSet-Cookie: h=1; Domain=example.com\r\nSet-Cookie: i=1; Domain=example.com  \r\n\r\n` and `-sv -b '' http://127.0.0.1:PORT/`: the three refusals are, byte for byte on standard error,
  - `* skipped cookie with bad tailmatch domain: example.com; Path=/\r\r\n\r\n`
  - `* skipped cookie with bad tailmatch domain: example.com\r\r\n\r\n`
  - `* skipped cookie with bad tailmatch domain: example.com  \r\r\n\r\n`
  (Windows' text-mode standard error turns each `\n` into `\r\n`, so the text curl wrote was `...\r\n\n`.) Curl writes the same lines ending `\r\n` once, with no empty line after them; every other line of the run matches.
- Curl today: `Curl.Cookies.UnitLibrary/SetCookieParser.cs` line 414 builds the refusal from `headerValue[(part.ValueStart + dotLength)..]`; `headerValue` reaches `SetCookieParser.Parse` without its line ending (from `CookieStore`, `ParseReportingRefusal`). `Curl.Output`'s `VerboseTransferEventWriter` already turns each `\n` into CR LF on Windows, so a refusal text ending `\r\n` comes out as curl's bytes.
- A header line curl receives with a bare `\n` would print only `\n`; the header value no longer says which ending it had, and HTTP/1.1 servers, HTTP/2 and HTTP/3 (curl rebuilds those lines with `\r\n`) all give CR LF, so append `\r\n` and note the bare-LF case in the parser's doc comment.

## Acceptance criteria

- [x] Tests in `Curl.Cookies.UnitTests` assert the refusal text for each of the three measured headers is `skipped cookie with bad tailmatch domain: ` followed by the measured rest of the line and `\r\n` (e.g. `...: example.com; Path=/\r\n`), from `SetCookieParser.Parse(..., out refusal)` and from the `CookieStore` path that reports it as an info line.
- [x] Existing tests that pinned the refusal without the line ending are updated, and their comments cite `lib/cookie.c` lines 518-525 and the 2026-10-02 measurement.
- [x] No other refusal text changes: a test pins `invalid cookie, dropped` and `skipped cookie because not 'secure'` unchanged.
- [x] `ParseFromCookieFile` (a `Set-Cookie:` line read from a `-b` file) keeps its current text: a test pins it, since that path does not come from a received header.
- [x] `dotnet build Curl.Cookies.UnitTests -warnaserror` is clean; `dotnet test Curl.Cookies.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cookies.UnitLibrary` reports no failing member.

## Notes

- The `-b` file case was not measured; if a measurement shows curl prints the file line's own ending there too, file a follow-up rather than widening this task.
- Delivered directly (a one-line change): `TrySetDomain` appends `\r\n` to the refusal; doc comments on it and on `Parse(..., out refusal)` cite `lib/cookie.c` 518-525 and the bare-LF case. `ParseFromCookieFile` never reaches the tailmatch refusal (no host), so its text is unchanged by construction; `ParseFromCookieFile_DomainLine_KeepsItsRefusalText` pins it.
- Quality was measured from a Cookies-only coverage run (`dotnet test Curl.Cookies.UnitTests --collect "Code Coverage;Format=cobertura"`, then `Measure-CodeQuality.ps1 -Library Curl.Cookies.UnitLibrary -SkipTestRun -ResultsDirectory ...`): 100% line, 100% branch, 0 failing members. The script's default whole-solution run exceeds 30 minutes in a lane. 363 tests pass.

## Log

- 2026-10-02: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Curl's -v bad tailmatch domain refusal ends with the header line's CR LF, as curl 8.21.0's does
