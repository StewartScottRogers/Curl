---
id: BL-205
title: Build multipart/form-data request bodies as a StreamBody
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-159, BL-189]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-205 — Build multipart/form-data request bodies as a StreamBody

## Goal

A multipart builder in `Curl.Core.UnitLibrary` turns BL-189's part specifications into a `StreamBody` with a computed Content-Length, byte-equal to curl.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item K3. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured `-u u:p -F a=b -F f=@srv.py`: `POST / HTTP/1.1\r\nHost: 127.0.0.1:18082\r\nAuthorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 949\r\nContent-Type: multipart/form-data; boundary=------------------------H5US3YN5uWKNbfycvXmtss\r\n\r\n--------------------------H5US3YN5uWKNbfycvXmtss\r\nContent-Disposition: form-data; name="a"\r\n\r\nb\r\n--------------------------H5US3YN5uWKNbfycvXmtss\r\nContent-Disposition: form-data; name="f"; filename="srv.py"\r\nContent-Type: text/plain\r\n\r\n<the file's bytes>\r\n--------------------------H5US3YN5uWKNbfycvXmtss--\r\n`.
- Boundary: `------------------------` + 22 random alphanumerics in the header; each delimiter is `--` + boundary. Inject the boundary source.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] With an injected boundary the body is byte-equal to the measured one; Content-Length is known before sending.
- [x] An unopenable file returns `CurlExitCode.ReadError` (26) with the measured message, at the moment curl reports it (measured).
- [x] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- Plan item: K3 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered (2026-09-26): `Curl.Core.UnitLibrary/Multipart` - `MultipartFormBodyBuilder(IFileSystem, Encoding, Func<string> createBoundary).BuildAsync(parts)` returns a `MultipartFormBuildResult` holding a `StreamBody` (`multipart/form-data; boundary=...`, length known unless a file stream cannot seek) or exit 26. Its own part model `MultipartFormPart`/`MultipartFormPartKind`, because `Curl.Cli` references `Curl.Core` and the builder cannot take BL-189's `FormPartSpecification`; BL-233 maps one onto the other. `MultipartPartHeaders` ports `Curl_mime_prepare_headers` (lib/mime.c at tag `curl-8_21_0`, read before porting); `ConcatenatedReadStream` streams the files; `MultipartBoundary.CreateRandom` makes curl's 24 dashes + 22 alphanumerics.
- Measured: curl 8.21.0 (`/mingw64/bin/curl`, Schannel) with `Record-CurlExchange.ps1 -Port 18205 -CurlArgs --no-progress-meter,-F,<spec>,...,http://127.0.0.1:18205/` in a scratch folder holding `f.py` (`hi` LF), `g` (`yy`), `h.png` (bytes 1, 2), `t.txt` (`tt`). Pinned byte for byte with curl's boundaries: `-F a=b -F f=@f.py` (Content-Length 305; `f.py` gets `application/octet-stream`, not the `text/plain` the Context's `srv.py` sample suggests); `-F a=@g -F b=@h.png -F c=<t.txt -F d=<f.py -F "e=@t.txt;type=x/y;filename=q" -F "h=v;headers=X-A: 1" -F =anon` (868); `-F a=@t.txt,g` (564, a `multipart/mixed` of `attachment` parts); `-F "m=(;type=multipart/alternative" -F x=1 -F y=@t.txt -F "=)" -F z=2` (632); `-F "m=(" -F x=1 -F "=)"` (386); `-F "m=(;type=multipart/form-data" -F x=1 -F "=)"` (389, inner parts `form-data`); `-F 'a"b\c=v' -F 'f=@t.txt;filename="q\"r"' -F 'c=<t.txt;filename=zz' -F 't=v;type=text/html'` (517, `%22` escapes); `-F "a=v;headers=content-type: x/z;headers=X-B: 2" -F "b=v;headers=Content-Disposition: inline" -F c=<h.png -F "d=v;filename=x.txt" -F "e=v;filename=x.py"` (638); `-F "né=vé"` sent `é` as the byte `E9` (code page 1252).
- Failures measured: `-F a=b -F f=@nope.txt` and `-F f=<nope.txt` -> exit 26, `curl: (26) Failed to open/read local data from file/application`, no connection opened (request.bin empty). `-F f=@<directory>` -> exit 26, `curl: (26) read error getting mime data`, after curl had sent a chunked request head with `Expect: 100-continue`.
- Decisions (ADR-0025, decided by Claude under Stewart's delegation): own part model; files opened at build time through `IFileSystem` and streamed; any failed open is exit 26 before sending (`FileAccessStatus.IsDirectory` with curl's `read error getting mime data`, the rest with `Failed to open/read ...`), so the directory case sends nothing where curl sends a head first; text encoded with an injected `Encoding` (ANSI code page on Windows, UTF-8 elsewhere, as ADR-0022). Default taken: nested boundaries are drawn from the same source, outermost first.
- `touches` widened with `Documentation/Planning/Decisions` for ADR-0025 and its index row; no task in Doing names it (BL-179, BL-192, BL-217, BL-221, BL-232 checked).
- Follow-ups filed: BL-269 (`;encoder=`), BL-270 (`@-`/`<-` standard input). Wiring into `Curl.Console` is BL-233, already filed.
- Gates: `dotnet build -warnaserror` clean; `dotnet test --filter "TestCategory!=Integration"` green (Curl.Core.UnitTests 402 passed, 2 skipped pre-existing); `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. MultipartFormBodyBuilder builds -F parts into curl 8.21.0's multipart/form-data StreamBody byte for byte with a known Content-Length, and an unopenable file is exit 26 before sending
